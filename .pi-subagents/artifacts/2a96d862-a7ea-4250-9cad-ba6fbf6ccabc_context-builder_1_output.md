# Inventory movement slice — implementation context

## Recommended smallest correct scope

Deliver **workspace-scoped quantity adjustments and on-hand balances**, not transfers, orders, reservations, lots/serials, valuation, or editing/deleting/reversing historical movements.

A user selects one active product and one active warehouse, submits a positive whole/decimal quantity and one of two immutable reasons:

* `Receipt` (`+quantity`) — establishes/increases stock.
* `Issue` (`-quantity`) — decreases stock, rejected if it would make that product/warehouse balance negative.

The slice returns the created immutable movement and its resulting on-hand quantity, lists current non-zero (or all) product-by-warehouse balances, and renders an Inventory page with a small adjustment form plus balances/recent movements. A transfer is deliberately deferred: it must atomically create two legs, lock two balance keys in deterministic order, and has different UI/API semantics.

This is the smallest useful ledger slice because a receipt alone cannot correct stock or demonstrate non-negative stock/concurrency protection, while a generic signed `delta` lets callers obscure business intent. Do not add purchase/sales integrations, users/roles, cost, reorder levels, pagination/filtering beyond a bounded recent list, or movement mutation/archival.

## Codebase evidence and affected files

| Area | Evidence | Implementation consequence |
|---|---|---|
| Architectural pattern | `README.md:22-45`; `backend/src/InventoryFlow.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs:20-30` | Preserve Api → Application/MediatR → Domain ← Infrastructure layering. New feature gets Application models/handlers/validator/interface and EF implementation. Registration/scanning is automatic for handlers/validators, but persistence interface requires explicit DI registration. |
| Tenant boundary | `backend/src/InventoryFlow.Api/Controllers/ProductsController.cs:12-57`; `backend/src/InventoryFlow.Application/Common/Tenancy/ICurrentWorkspace.cs:1-7`; `backend/README.md:13-16` | Controller resolves workspace from authenticated identity only; requests must never include/choose `workspaceId`. Every read/write query filters workspace. Cross-workspace IDs return 404, never reveal existence. |
| Existing catalog lifecycle | `Product.cs:7-64`; `Warehouse.cs:3`; `ProductConfiguration.cs:13-20`; `WarehouseConfiguration.cs:1` | Products/warehouses are workspace-owned and soft-archived; FK them from ledger/balance. Their archive endpoints currently have no stock awareness—see blocker below. |
| Persistence baseline | `ApplicationDbContext.cs:21-36`; `20260718115041_AddWarehouses.cs:16-44` | Add DbSets/configurations and an EF migration plus snapshot. SQL Server is the production/test database. |
| Conflict/error conventions | `EfProductCatalog.cs:13-30`; `GlobalExceptionHandler.cs:35-70` | Existing catalog conflicts use unique SQL index + feature exception → 409. Add movement/idempotency/insufficient-stock exceptions deliberately; do not let a SQL error become 500. |
| Retry behavior | `InfrastructureServiceCollectionExtensions.cs:75-81` | SQL Server has `EnableRetryOnFailure`; explicitly execute the entire explicit transaction through EF’s execution strategy (not a manual transaction outside it). |
| API test harness | `AuthenticatedApiFixture.cs:10-78`; `WarehouseEndpointsTests.cs:27-99` | Add a Testcontainers SQL Server `InventoryMovementEndpointsTests` using registration-derived bearer tokens and direct `ApplicationDbContext` assertions. |
| Frontend conventions | `ProductsPage.tsx:15-63`; `products-api.ts:1-11`; `router/index.tsx:48-58`; `lib/api-client.ts:1-59` | Feature owns API/types/schema/page; TanStack Query cache keys include `user.id` and server workspace ID. Axios already sends auth and refreshes only a 401 once. Replace existing `/inventory` placeholder with lazy feature page. |

Expected new/changed paths: `Domain/Entities/InventoryMovement.cs`, `Domain/Entities/InventoryBalance.cs`; Application `Features/Inventory/{InventoryModels,InventoryHandlers,InventoryValidators,IInventoryLedger}.cs`; Infrastructure `Inventory/EfInventoryLedger.cs`, `Persistence/Configurations/{InventoryMovementConfiguration,InventoryBalanceConfiguration}.cs`, DbContext/DI/migration/snapshot; API `Controllers/InventoryController.cs` and global exception mapping; integration and domain tests; frontend `features/inventory/{types.ts,inventory-api.ts,inventory-schema.ts,pages/InventoryPage.tsx}` and router. Product/Warehouse archive handlers/catalogs may also need changes below.

## Domain and persistence design

### Entities

`InventoryMovement` is append-only and has:

* `Id`, `WorkspaceId`, `ProductId`, `WarehouseId` (all required GUIDs);
* `Kind` enum `Receipt | Issue` (persist string or constrained tinyint); `Quantity decimal(18,4)` strictly `> 0`; `SignedQuantity` is derived (`+/-`), not client supplied;
* `OccurredAtUtc` from `TimeProvider`, `CreatedAtUtc` if separately useful (both UTC); optional trimmed `Note` with a small explicit max (e.g., 500);
* `IdempotencyKey` from the request header, max 100/128 chars, immutable; optional stored request fingerprint/payload fields sufficient to detect same-key/different-command reuse.

`InventoryBalance` is the transactionally maintained projection: `WorkspaceId`, `ProductId`, `WarehouseId`, `Quantity decimal(18,4) NOT NULL`, optionally a rowversion for diagnostics. It has a surrogate `Id` or composite PK; use a unique index on `(WorkspaceId, ProductId, WarehouseId)` regardless. A balance is **not** the source of audit truth; it is a materialized, lockable invariant projection of the immutable movement ledger.

Configuration requirements:

* Explicit `decimal(18,4)` for movement quantity and balance quantity. Never use float/double.
* FKs to `Workspaces`, `Products`, `Warehouses`; choose `Restrict/NoAction` for product/warehouse once movements exist (ledger history must not cascade-delete). Workspace cascade remains consistent with existing tenant model if desired.
* unique `(WorkspaceId, IdempotencyKey)` on movements; index list query `(WorkspaceId, OccurredAtUtc, Id)`; unique balance key plus useful `(WorkspaceId, WarehouseId, ProductId)` lookup index if the unique order differs.
* Add a migration generated from the current model; do not hand-edit only the snapshot.

### Non-negotiable invariants

1. Movement, balance, product, and warehouse always share the same server-resolved workspace. Do not rely on client IDs or FK IDs alone for tenancy.
2. Every accepted adjustment appends exactly one movement and changes exactly its matching balance by its derived signed amount in the **same transaction**. A rejected request changes neither.
3. `Quantity > 0`, decimal precision is bounded, timestamps are UTC, and ledger fields have no public update/delete path.
4. A balance may never fall below zero. The sum of committed signed ledger quantities for a balance key equals that stored balance (migration starts empty, so no opening reconciliation complexity).
5. A movement can target only active product and warehouse records. Archiving a product/warehouse with any non-zero balance must return conflict; otherwise archived catalog records could strand inventory. Existing archive behavior must be modified and tested accordingly.
6. The same idempotency key in one workspace is exactly-once: same normalized command returns the original successful response; a differing command returns 409; the key may be reused in another workspace.

## API contract

Use a dedicated controller, `[ApiController] [Authorize] [Route("api/inventory")]`, following product controllers’ workspace resolution. Suggested endpoints:

* `POST /api/inventory/movements`, required `Idempotency-Key` header (validate nonempty/max length) and body `{ productId, warehouseId, kind: "receipt" | "issue", quantity, note? }`. Return `201 Created` for first execution and `200 OK` for an idempotent replay (or consistently `201` only if the team documents that convention). Response: `{ id, productId, productName, warehouseId, warehouseName, kind, quantity, signedQuantity, occurredAtUtc, note, resultingQuantity }`.
* `GET /api/inventory/balances` returns workspace-scoped balance rows including product/warehouse display names, `quantity`, and whether source catalog records are archived if needed for historical visibility. Start with all non-zero balances ordered warehouse/product.
* `GET /api/inventory/movements?take=50` is optional but recommended if the UI claims “recent activity”; validate a bounded take and order deterministic `OccurredAtUtc desc, Id desc`. It is not needed to make the adjustment form correct.

Validation: GUIDs nonempty, enum known, `quantity > 0` and scale ≤ 4 (reject rather than silently round), note length, and header format. `404` applies to absent/cross-workspace/archived product or warehouse. `409` applies to insufficient stock, reused key with a different command, and archive with nonzero stock. Existing `GlobalExceptionHandler.cs:35-70` needs these mappings/titles instead of reporting all conflicts as catalog-value conflicts.

## Concurrency and idempotency implementation design

**Do not implement read balance → calculate → normal tracked SaveChanges.** Parallel Issues can both see the same balance and oversell.

For first implementation, encapsulate the entire command in `EfInventoryLedger.RecordAsync` so the application handler has no partial persistence operations:

1. Validate server workspace and active product/warehouse within the database operation.
2. Obtain `db.Database.CreateExecutionStrategy()` and run the whole explicit `Serializable` transaction inside `ExecuteAsync`. EF Core’s SQL Server retry setting makes this necessary for retry-safe transaction execution.
3. At the beginning, read a pre-existing `(workspace,key)` movement. If it exists, compare canonical command fields/fingerprint and return it without touching balance; mismatch is a domain 409.
4. Acquire update/range locks for the balance key (`UPDLOCK, HOLDLOCK`/serializable key lookup in SQL Server) before deciding whether to insert its zero row or update it. Use the unique balance key. Lock/read the active product and warehouse state in the same transaction so archive cannot race a new movement; archive must use compatible locks/checks.
5. Apply the delta atomically. For an Issue, conditionally update only if `Quantity >= requested`; zero rows affected is `InsufficientStockException`. For a Receipt, create/update the balance. Append the movement and commit atomically.
6. On unique-key collision for the idempotency index, roll back/discard failed tracked state, reread in a fresh transaction/context, and return original only if fingerprint matches. Never retry by executing an unkeyed movement again.

Using SQL Server locking/raw command for the small critical section is safer than EF tracked entities alone. If `ExecuteUpdate` is used, check its affected-row count: Microsoft notes it bypasses change tracking and does not automatically enforce optimistic-concurrency behavior. Avoid `MERGE` as an initial upsert solution. Keep transaction retry/idempotency tests focused on observable exactly-once outcome, not implementation detail.

Authoritative implementation references: Microsoft Learn, [EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions), [connection resiliency](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency), [efficient updating](https://learn.microsoft.com/en-us/ef/core/performance/efficient-updating), and SQL Server [locking guide](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide?view=sql-server-ver17) (`HOLDLOCK` is serializable for the table reference).

## Frontend arc

Replace `frontend/src/app/router/index.tsx:53` placeholder with a lazy `InventoryPage`. Follow the feature boundaries and query-key isolation in `ProductsPage.tsx:15-63`:

* Query active products and warehouses for selectors (avoid pages that use cross-workspace cache keys); query inventory balances and optionally recent movements with `['inventory', 'balances', userId, workspaceId]` / movement equivalent.
* Schema aligns with API: UUID product/warehouse IDs, enum kind, positive quantity with ≤4 decimals, note maxlength. Use a string input converted deliberately—JavaScript number arithmetic must not be used to calculate/display authoritative quantity.
* Form disables duplicate click while pending, creates one `crypto.randomUUID()` idempotency key per submission attempt, and passes it as `Idempotency-Key`. Preserve that key if user retries after timeout/network failure; generate a fresh one only after a completed response or intentional new command.
* On success invalidate balances/movements (not product/warehouse keys); show API ProblemDetails error for 404/409/validation. Render available quantity and a receipt/issue selector; do not optimistically alter balance because server-side concurrency may reject an Issue.

## Test matrix / validation

Domain unit tests: invalid IDs/UTC/kind/quantity/scale; signed delta derived correctly; immutable movement/no mutation API; balance cannot become negative.

SQL Server integration tests (model after `WarehouseEndpointsTests.cs:27-99`):

1. unauthenticated is 401; workspace isolation allows same idempotency key/balance keys across two workspaces and cross-tenant product/warehouse IDs are 404;
2. receipt creates one ledger row and correct balance; issue reduces it; an over-issue is 409 with no new ledger row/no balance change;
3. duplicate same key/payload yields one row and one balance change; same key altered quantity/kind/product/warehouse is 409; concurrent duplicate requests still one row/change;
4. concurrent Issues competing for limited stock produce only quantities whose aggregate does not exceed stock, no negative balance, and one/more 409 responses as appropriate;
5. movement rejects archived product/warehouse; archive rejects a product/warehouse with a nonzero balance (and test archive/movement race if implementation exposes lock strategy);
6. foreign keys/restrict delete and decimal model mapping are migration-backed.

Run after implementation:

```sh
dotnet build backend/InventoryFlow.sln
dotnet test backend/InventoryFlow.sln
dotnet format backend/InventoryFlow.sln --verify-no-changes --no-restore
cd frontend && bun run typecheck && bun run lint && bun run build && bunx prettier --check .
```

Docker/Testcontainers is required for current integration tests; if unavailable, report that explicitly rather than calling unrun concurrency tests passing.

## Review findings

* **High — required scope correction:** existing archive handlers (`ProductHandlers.cs:38-50` and warehouse counterpart) blindly archive. With balances, this permits a non-zero stock position to become unmovable because movements must reject archived catalog records. Add nonzero-balance conflict semantics and compatible locking before claiming a correct movement slice.
* **High — implementation risk:** `InfrastructureServiceCollectionExtensions.cs:79-81` enables retry-on-failure. An explicit transaction implemented outside an EF execution strategy can fail/retry incorrectly; a retry-safe, idempotency-keyed transaction boundary is required.
* **High — implementation risk:** no existing balance projection or concurrency token exists (`ApplicationDbContext.cs:21-31`). A handler that reads then writes tracked quantity will oversell under parallel issues; enforce the invariant at the SQL transaction/conditional-update level.
* **Medium — existing adjacent UI defect:** `frontend/src/features/warehouses/pages/WarehousesPage.tsx:96-112` duplicates the `name` input. Avoid copying that page verbatim; Inventory needs distinct labels and one control per field.
* **Medium — current global error text:** `GlobalExceptionHandler.cs:63` labels every 409 as a catalog-value conflict. Movement 409s need accurate problem titles/details.

## Residual risks / decisions recorded

* Explicit movement correction/reversal is deferred. This preserves audit immutability but operational correction must be a compensating Receipt/Issue until a separately designed reversal feature exists.
* `decimal(18,4)` is proposed because products currently have no unit-of-measure model. Confirm precision with product requirements before implementation; never silently round.
* Archiving/movement lock compatibility is the subtle part of correctness. If the team cannot implement/test it in this slice, block archival of any product/warehouse that has ever had a movement as a conservative alternative; do not leave the current race unaddressed.
* The post-warehouse worktree is clean except the runtime-owned untracked `.pi-subagents/` directory (`git status --short`); no source edits were made by this context task.

## Meta-prompt handoff

> **Goal:** Implement the first workspace-scoped inventory movement vertical slice: immutable Receipt/Issue adjustments for active products in active warehouses, SQL Server-backed on-hand balances, current-balance UI, and exactly-once API behavior.
>
> **Evidence/constraints:** Follow `ProductsController`/`ProductHandlers`/`EfProductCatalog` layering and server-resolved `ICurrentWorkspace`; do not accept `workspaceId`. SQL Server + EF Core 9 is configured with retry-on-failure. Products and warehouses are soft-archived, so movement must reject archived sources and archive must conflict when nonzero stock exists. `decimal(18,4)`, quantity > 0, and append-only ledger are hard invariants. Exclude transfers, orders, reservations, costing, lots/serials, edit/delete/reversal, and product UOM redesign.
>
> **Success criteria:** A POST with active product/warehouse and `Idempotency-Key` records exactly one immutable movement and atomically updates exactly one scoped balance; Issue never makes it negative; same workspace/key/same command replays safely and different command conflicts; all tenant/cross-tenant and archived behavior is correct; `/inventory` is a working authenticated page with form/balances; migration and focused domain/integration/frontend checks pass.
>
> **Suggested approach:** Add domain movement/balance, Application command/query/validator/interface, EF configuration/repository and migration, API controller/error mapping, then frontend feature and lazy route. Put the entire ledger + balance operation in one execution-strategy-managed serializable SQL Server transaction with unique balance and idempotency indexes, compatible archive locks/checks, and affected-row checking. Prefer a bounded recent list only if rendered.
>
> **Validation:** Add Testcontainers tests for tenant isolation, receipt/issue/overissue, idempotent replay/conflict/concurrency, concurrent issues, and archive behavior; run solution build/test/format and frontend typecheck/lint/build/prettier. Report unavailable Docker rather than masking it.
>
> **Stop/escalate:** Stop and ask the supervisor before changing scope to transfers/UOM/costing/reversal, choosing a quantity precision other than documented `18,4`, or weakening archive protection. Stop when requested code/tests are complete; do not refactor unrelated catalog/UI defects (the duplicate warehouse field may be noted but is out of scope).