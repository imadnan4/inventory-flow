# Local context: smallest workspace-scoped purchasing slice

## Recommended P0 boundary

Implement **posted purchase receipts only**: a protected `/purchases` UI and a workspace-derived API that records one supplier/product/warehouse/positive quantity receipt as an immutable purchase-receipt document and exactly one existing `InventoryMovementType.Receipt`. Include receipt history only to make the created document visible. Do **not** implement purchase orders, draft/approval state, multi-line documents, partial receipts, prices/costing, taxes, payment, supplier-product setup, or edits/cancellation.

This is intentionally distinct from the existing generic `/api/inventory/receipts` adjustment endpoint. It consumes the already-complete supplier catalog and creates an auditable supplier-linked document while retaining the inventory ledger as the authoritative balance writer.

## Evidence and relevant code

| Area | Evidence | Implication |
|---|---|---|
| Workspace boundary | `backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs:12-22` derives exactly one Owner workspace from the authenticated membership; controllers resolve it and never accept a workspace ID, e.g. `SuppliersController.cs:19-24`. | Purchase commands/query must take the server-resolved workspace; request DTOs must not carry `workspaceId`. Return `403` if the resolver yields null. |
| Catalog prerequisites | `Supplier.cs:13-45`, `Product.cs:15-59`, and `Warehouse.cs:4` own workspace IDs and archival timestamps. Active lists filter archived records in `EfSupplierCatalog.cs:28-32`, `EfProductCatalog.cs:29-31`, and `EfWarehouseCatalog.cs:29-32`. | Receipt posting must prove supplier, product, and warehouse are all in the current workspace and unarchived in the posting transaction. Historical receipts may continue to reference subsequently archived sources. |
| Ledger correctness | `EfInventoryLedger.cs:13-57` validates, runs an EF retry strategy + `Serializable` transaction, checks active product/warehouse, changes `InventoryBalances`, inserts an immutable movement, and commits. `InventoryMovementConfiguration.cs:15-24` has DB-unique `(WorkspaceId, IdempotencyKey)`. | Do not increment `InventoryBalances` from a purchasing handler or a controller. Purchase posting must reuse/refactor this single ledger writer and remain in the same database transaction as the purchase document. |
| Ledger contract | `IInventoryLedger.cs:6-16`; `InventoryMovement.cs:9-85`; `InventoryBalance.cs:6-40`. Quantity is positive, `decimal(18,4)`, max `99999999999999.9999`, and existing retries return the original movement for a workspace/key. | Apply the identical quantity constraints. Do not add a purchase-specific balance calculation or another idempotency namespace that can collide with arbitrary client keys. |
| Existing API behavior | `InventoryController.cs:15-28` accepts `Idempotency-Key` header with body fallback and returns `201` even for an existing movement. `InventoryValidators.cs:18-28` enforces required IDs/key and scale/maximum. | Mirror header-first idempotency and `201`-on-replay behavior for the new receipt endpoint; frontend must retain the same generated key when explicitly retrying. |
| SQL schema / migration convention | `ApplicationDbContext.cs:21-45` exposes sets; configuration is assembly scanned at lines 47-53. Latest migration is `20260718130340_AddSuppliers.cs`; snapshot is `ApplicationDbContextModelSnapshot.cs`. | Add entities, DbSet/configurations, then generate a normal EF migration and snapshot/designer update; do not hand-edit only the snapshot. SQL Server is the production/test provider. |
| Exception/API conventions | `GlobalExceptionHandler.cs:38-88` maps Fluent/domain violations to 400 and known conflict exceptions to 409. Missing source inventory returns null and controller maps to 404. | Invalid receipt input should be 400; missing/cross-workspace/archived supplier, product, or warehouse should be indistinguishable 404; normal retry is a successful replay, not 409. |
| Test infrastructure | `AuthenticatedApiFixture.cs:24-69` migrates a real SQL Server Testcontainer. `InventoryEndpointsTests.cs:29-56` proves receipt replay only makes two movements total after a separate issue; lines 146-197 cover archive/receipt races. | New endpoint tests must run against this fixture, not EF InMemory, and assert DB row counts and balance as well as status/body. |
| Frontend patterns | `frontend/src/features/inventory/pages/InventoryPage.tsx:17-123` has identity/workspace React Query keys, catalog dropdowns, Zod validation, UUID key creation, and a saved retry payload. `SuppliersPage.tsx:15-62` is the basic catalog page pattern. | Add a focused `features/purchases` API/types/schema/page. All new keys must include `user.id` and `user.workspace.id`; a retry must reuse its original idempotency key, not generate another. |
| Route | `frontend/src/app/router/index.tsx:53-65` protects the dashboard children; `/purchases` is currently a placeholder. Sidebar already links it (`frontend/src/components/layout/Sidebar.tsx:29-40`). | Replace only the purchases placeholder with a lazy `PurchasesPage`; no sidebar change is required. |

## Implementation design decisions

### Data model

Add a single immutable `PurchaseReceipt` domain entity (suggested `backend/src/InventoryFlow.Domain/Entities/PurchaseReceipt.cs`) with:

- `Id`, `WorkspaceId`, `SupplierId`, `WarehouseId`, `ProductId` (non-empty GUIDs).
- `Quantity` using `InventoryMovement.ValidateQuantity` / `MaxQuantity` semantics.
- `IdempotencyKey` normalized with the same trim/1..100 rule as `InventoryMovement`.
- `InventoryMovementId` (non-empty GUID) and `ReceivedAtUtc` (UTC). This creates the explicit audit link from purchase document to its one ledger entry.

Suggested configuration/table `PurchaseReceipts`:

- `decimal(18,4)` quantity; required string `nvarchar(100)` idempotency key; required timestamp.
- Workspace FK cascade; supplier, warehouse, product, and movement FKs `Restrict`.
- unique `(WorkspaceId, IdempotencyKey)` — one externally retriable receipt document per workspace/client key;
- unique `InventoryMovementId` — one movement cannot back two documents;
- listing index `(WorkspaceId, ReceivedAtUtc, Id)`.

The transaction must explicitly verify that all three catalog records are active and belong to `WorkspaceId`; FKs alone cannot enforce matching workspace IDs. The receipt should not be mutable or archivable in this scope.

### Atomic/idempotent posting requirement

**Do not implement this sequence:** save a receipt, then call current `IInventoryLedger.RecordAsync`, then update the receipt with the movement ID. Current `EfInventoryLedger.RecordAsync` opens and commits its own serializable transaction (`EfInventoryLedger.cs:20-56`), so that sequence permits an orphan receipt, a movement without its receipt link, or a retry-dependent split state.

Instead, refactor the infrastructure so purchase-document insertion, active-source checks, balance mutation, movement insertion, and linkage commit in **one** execution-strategy retry delegate and one `Serializable` transaction. There should be one reusable internal ledger write path; do not duplicate the balance algorithm in a purchase repository.

A concrete safe shape is:

1. Add an application `IPurchaseReceiptService`/catalog and a `RecordPurchaseReceiptCommand` that calls it. Its infrastructure implementation owns the outer strategy/serializable transaction.
2. Extract the current ledger's active product/warehouse lookup, balance update, movement creation, and idempotency lookup into a transaction-aware internal writer used by both direct inventory receipts and purchase receipts. The writer must not open/commit a nested transaction when invoked by purchase posting.
3. For a new purchase receipt, generate both receipt ID and movement ID before persistence. Use `receipt.Id.ToString("N")` as the internal ledger idempotency key (32 characters), not the raw client key. This avoids a collision with the existing generic inventory API's workspace-global client key while making the ledger key deterministic for that document.
4. First lookup `PurchaseReceipts` by `(workspaceId, normalized client idempotency key)`. If present, return its persisted receipt response immediately; do not validate/repost based on possibly changed request fields. For new records, add document and movement with their IDs linked, write balance, save once, commit once.
5. Preserve the existing unique ledger key on `InventoryMovements`; it is a second durable guard. The unique purchase document key is the durable replay guard. Handle a SQL Server unique race defensively by clearing/reloading/re-querying the winning document (or prove the serializable range read prevents it), never by creating a second movement.

The existing ledger’s unique key is workspace scoped (`InventoryMovementConfiguration.cs:20`), so the generated internal key plus the unique receipt key allow the same browser-supplied key in different workspaces and prevent repeat stock in the same workspace.

### API contract

Suggested feature files under `Application/Features/Purchases/` and controller `Api/Controllers/PurchaseReceiptsController.cs`:

- `POST /api/purchases/receipts`
  - Body: `{ supplierId, warehouseId, productId, quantity, idempotencyKey? }`; `Idempotency-Key` header wins when supplied, matching `InventoryController.cs:20-27`.
  - Response 201: `{ id, supplierId, warehouseId, productId, quantity, inventoryMovementId, receivedAtUtc }` for both first post and replay.
  - 400 invalid IDs/quantity/key; 403 missing/ambiguous workspace; 404 unavailable/cross-workspace/archived source. No 409 is required for a normal duplicate/retry.
- `GET /api/purchases/receipts`
  - Workspace-scoped, newest `ReceivedAtUtc` then `Id`; returns the same response list. This is the minimum history needed for the UI/audit proof. No filters or pagination in P0.

Use MediatR DTO/validator/handler conventions from `Features/Inventory` and register the new infrastructure service in `InfrastructureServiceCollectionExtensions.cs:79-83`. Add `DbSet<PurchaseReceipt>` in `ApplicationDbContext.cs` and map the new domain rules as 400 through existing `DomainException` behavior.

### Frontend P0

Create `frontend/src/features/purchases/{types.ts,purchases-api.ts,purchases-schema.ts,pages/PurchasesPage.tsx}` and lazy-load it at `/purchases`.

The page should:

- load active suppliers, products, warehouses, and receipt history only for an authenticated identity/workspace;
- offer exactly Supplier, Warehouse, Product, and Quantity selectors/input and a “Receive purchase” submit;
- validate UUID selections and positive 4-decimal quantity before POST;
- create `crypto.randomUUID()` only for a new submit; store the submitted payload including its key on error and send exactly that payload for Retry;
- invalidate workspace-scoped receipt history and `['inventory', 'balances', userId, workspaceId]` after success, because a purchase receipt changes on-hand stock;
- show loading, empty, and ProblemDetails error states following `InventoryPage.tsx:42-55` and `SuppliersPage.tsx:77-107`.

Do not change the generic Inventory page as part of this minimal slice unless required to avoid a type/route conflict. It remains a direct inventory adjustment UI; the Purchases page is the only supplier-linked receipt UI.

## Target test plan

1. **Domain unit tests** (`backend/tests/InventoryFlow.UnitTests/Domain/PurchaseReceiptTests.cs`): required IDs, UTC date, quantity precision/range, idempotency normalization/length, immutable exposed state.
2. **SQL Server endpoint tests** (`backend/tests/InventoryFlow.IntegrationTests/Api/PurchaseReceiptEndpointsTests.cs`):
   - unauthenticated POST/GET is 401;
   - one valid receipt is 201, persists exactly one receipt and one linked `Receipt` movement, and increases exactly the correct balance;
   - sequential same-key replay returns the original document and leaves receipt/movement count and balance at one receipt;
   - concurrent same-key POSTs (two API clients/tasks) both resolve successfully to one document/movement and one balance increment; assert actual DB counts;
   - same client key in two registered workspaces succeeds independently; cross-workspace IDs give 404 and create no rows;
   - archived supplier/product/warehouse is rejected with 404 and no document/movement/balance side effects;
   - invalid scale/zero/over-max/key produces 400 and no side effects;
   - GET lists only current-workspace receipts newest-first.
3. Keep existing `InventoryEndpointsTests` passing to prove direct receipt/issue behavior and source archive serialization was not regressed.
4. Frontend has no established test runner. Validate typecheck, lint, production build, and Prettier verification.

## Review findings

- **High — `backend/src/InventoryFlow.Infrastructure/Inventory/EfInventoryLedger.cs:20-56`:** The current ledger owns and commits its transaction. Calling it after independently inserting a purchase receipt is non-atomic and can duplicate or orphan business/audit state. Refactor around a single transaction-aware writer; do not stitch together separate commits.
- **High — `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/InventoryMovementConfiguration.cs:20`:** Idempotency keys are unique for every movement in a workspace, not just by endpoint/type. Reusing a browser purchase key directly can resolve to an unrelated direct inventory movement. Persist a separate purchase replay key and use a generated deterministic internal movement key.
- **Medium — `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/InventoryBalanceConfiguration.cs:14-19`:** The balance is materialized and composite-keyed. Any purchasing implementation that directly updates it risks bypassing validation/concurrency behavior. Reuse the ledger path only.
- **Medium — `frontend/src/features/inventory/pages/InventoryPage.tsx:99-123`:** A retry is safe only because it retains `retryMovement`, including idempotency key. A purchases page must not regenerate the UUID during retry.
- **Medium — `backend/src/InventoryFlow.Infrastructure/Suppliers/EfSupplierCatalog.cs:40-51`:** Supplier archival is idempotent and does not yet have purchase awareness. P0 may allow archival of a supplier with historical receipts, but receipt posting must check it is currently active inside its serializable transaction.

## Meta-prompt handoff

**Goal:** Implement the minimal workspace-scoped purchase-receipt vertical slice described above: supplier-linked single-line posted receipts that atomically create one immutable purchase receipt and one receipt ledger movement, update on-hand once, safely replay duplicate submissions, expose protected create/list APIs, and replace the protected `/purchases` placeholder with a usable receive/history UI.

**Context/evidence:** Follow the concrete files and line references in this handoff. Existing source catalogs are workspace scoped and soft-archived; `EfInventoryLedger.cs:13-57` is the established balance/idempotency algorithm but currently self-owns its transaction. API conventions are MediatR + FluentValidation + global ProblemDetails; integration tests use real SQL Server Testcontainers. The frontend patterns are in `InventoryPage.tsx` and `SuppliersPage.tsx`; all React Query keys must include user and workspace identity.

**Success criteria:**
- A receipt has supplier, warehouse, product, positive `decimal(18,4)` quantity, current workspace, timestamp, client replay key, and a one-to-one immutable inventory movement link.
- POST replay/concurrency for the same workspace/client key yields one persisted receipt, one movement, and one balance increment; same key in another workspace is independent.
- Receipt document + ledger movement + balance are committed/rolled back atomically under the existing retry/serializable approach; generic direct inventory receipt/issue behavior remains intact.
- Active workspace-local supplier/product/warehouse are mandatory; unavailable/cross-workspace/archived sources cause 404 without effects.
- `/purchases` is protected, functional, retry-safe, identity/workspace cache isolated, and does not introduce ordering, partials, costs, taxes, or approvals.

**Hard constraints:** Do not accept `workspaceId` from HTTP input. Do not duplicate/independently mutate the balance logic. Do not use a raw browser purchase key as the ledger movement key. Do not make document posting and ledger posting separate commits. Do not add the excluded purchasing capabilities. Do not modify existing migration history; add a new migration.

**Suggested approach:** Add the domain/persistence/API feature, refactor ledger persistence only as needed to share a transaction-aware write core, generate the migration, then add SQL Server integration coverage before adding the small frontend feature and route. Keep the existing direct inventory endpoints intact.

**Validation:**
```bash
dotnet build backend/InventoryFlow.sln --no-restore
dotnet test backend/InventoryFlow.sln --no-restore
dotnet format backend/InventoryFlow.sln --verify-no-changes --no-restore
cd frontend && npm run typecheck && npm run lint && npm run build
# If package manager policy uses Bun, use equivalent bun commands consistent with the lockfile.
```
Run the new concurrent receipt test repeatedly if practical. Also inspect the generated migration and model snapshot for the two unique indexes, all FKs, `decimal(18,4)`, and no accidental modification of older migrations.

**Stop/escalation:** Escalate before changing the P0 boundary (multi-line/partial receipts, orders, costs/taxes/approval, deleting historical data) or if atomic transaction sharing cannot be implemented without a design choice. Otherwise proceed without further product questions. Stop after tests/build/format checks pass and report any environment-only test limitation explicitly.

**Resolved assumptions:** P0 is a posted, single-line goods receipt (not a purchase order); an archived supplier remains valid historical reference but cannot receive new stock; existing direct inventory receipt is retained as a generic adjustment; duplicate retries return the already-created receipt with 201, matching the current inventory endpoint.

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "Concrete severity-ranked findings cite EfInventoryLedger.cs:20-56, InventoryMovementConfiguration.cs:20, InventoryBalanceConfiguration.cs:14-19, InventoryPage.tsx:99-123, and EfSupplierCatalog.cs:40-51; residual risks are listed below."
    }
  ],
  "changedFiles": [
    ".pi-subagents/artifacts/outputs/d2aa83fa-dc83-4865-a542-05aa3ff0ff01/parallel-0/1-context-builder/context.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "git status --short && git log --oneline -5 && targeted nl/read inspection",
      "result": "passed",
      "summary": "Confirmed source baseline is committed through supplier catalog; only runtime .pi-subagents artifacts are untracked, and inspected ledger/API/catalog/test/frontend evidence."
    }
  ],
  "validationOutput": [
    "No product code was edited by this context-only task.",
    "Implementation validation commands and targeted Testcontainers cases are specified in the handoff."
  ],
  "residualRisks": [
    "The current ledger's self-owned transaction must be refactored carefully; a naive receipt-save then ledger-call design is not atomic.",
    "Concurrent duplicate behavior requires a real SQL Server integration test with persisted receipt, movement, and balance counts.",
    "Supplier archival remains historically allowed; the new posting transaction must verify active supplier state."
  ],
  "noStagedFiles": true,
  "diffSummary": "Only the required context/meta-prompt artifact was written; no application, migration, frontend, or test source was edited.",
  "reviewFindings": [
    "high: backend/src/InventoryFlow.Infrastructure/Inventory/EfInventoryLedger.cs:20-56 - independent purchase and ledger commits would violate atomic receipt posting.",
    "high: backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/InventoryMovementConfiguration.cs:20 - raw client keys can collide across direct and purchase receipt entry points.",
    "medium: backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/InventoryBalanceConfiguration.cs:14-19 - purchase code must not duplicate balance mutation.",
    "medium: frontend/src/features/inventory/pages/InventoryPage.tsx:99-123 - retries must retain their original idempotency key."
  ],
  "manualNotes": "Scope and implementation contract are complete; no source edits were performed."
}
```