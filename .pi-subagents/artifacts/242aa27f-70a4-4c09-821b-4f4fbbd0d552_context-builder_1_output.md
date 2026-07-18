# Warehouse-transfer slice: implementation context

## Recommended bounded outcome
Add a workspace-scoped, posted **single-product warehouse transfer**: source warehouse, destination warehouse, product, positive `decimal(18,4)` quantity, retry-stable idempotency key, and server UTC timestamp. A successful POST atomically creates one immutable transfer document, **exactly two immutable existing `InventoryMovement` rows** (source `Issue`, destination `Receipt`), and updates both materialized balances. Add newest-first transfer history plus a protected `/transfers` UI with durable retry and balance/history cache invalidation.

Do **not** model this as two client calls or two independent calls to `InventoryLedgerWriter.RecordAsync`: a partial issue/receipt breaks conservation and no single existing call locks the pair deterministically.

## Current architecture and exact evidence

### Backend flow and conventions
- `backend/src/InventoryFlow.Domain/Entities/InventoryMovement.cs:8-66`: only `Receipt` and `Issue` movement types exist; movement is immutable by construction, validates a positive <=4-place quantity and UTC timestamp, and has a 100-character normalized key. Transfers should use these two types, not add a `Transfer` type.
- `backend/src/InventoryFlow.Domain/Entities/InventoryBalance.cs:5-42`: balance key is workspace/warehouse/product and `Apply` rejects negative stock and overflow. Source must call `Apply(-quantity)` before creating either document/movement; a throw must roll back every pending change.
- `backend/src/InventoryFlow.Infrastructure/Inventory/EfInventoryLedger.cs:15-27`: supported transaction pattern is `CreateExecutionStrategy().ExecuteAsync` around a new `Serializable` transaction. The SQL Server provider retry option is configured at `.../DependencyInjection/InfrastructureServiceCollectionExtensions.cs:80-81`.
- `backend/src/InventoryFlow.Infrastructure/Inventory/InventoryLedgerWriter.cs:55-76`: existing writer first treats `(WorkspaceId, IdempotencyKey)` as movement-level replay, validates active warehouse/product, loads/creates **one** balance, applies one direction, and queues one movement. It is suitable for receipt/issue and document services but is not a two-row transfer primitive.
- `backend/src/InventoryFlow.Infrastructure/Sales/EfSalesFulfillmentService.cs:16-45` is the closest document template: validate, execution-strategy + serializable transaction, document-level idempotency lookup, create a private movement key from the document ID, write immutable document and movement, save once, commit. `EfPurchaseReceiptService.cs:16-60` follows the same pattern and adds related-source validation.
- Application pattern: `backend/src/InventoryFlow.Application/Features/Sales/SalesFulfillmentModels.cs`, `SalesFulfillmentHandlers.cs`, and `SalesFulfillmentValidators.cs`; protected controller pattern is `backend/src/InventoryFlow.Api/Controllers/SalesFulfillmentsController.cs:10-40`. Controller resolves workspace only through `ICurrentWorkspace`; client request must never provide workspace ID. Header `Idempotency-Key` overrides body key.
- `backend/src/InventoryFlow.Api/ExceptionHandling/GlobalExceptionHandler.cs:37-43` converts `InsufficientInventoryException` to HTTP 409. Validation/domain exceptions are HTTP 400. A same-warehouse transfer needs explicit rejection (400) because it otherwise produces a semantically meaningless pair on one balance.
- `backend/src/InventoryFlow.Infrastructure/Products/EfProductCatalog.cs:42-73` and `.../Warehouses/EfWarehouseCatalog.cs:43-73` archive in serializable execution-strategy transactions and prohibit archival with nonzero on-hand balance. Transfer must ensure **both** warehouses and product belong to the workspace and are unarchived inside its serializable transaction, preserving the same concurrency invariant.

### Persistence constraints
- `backend/src/InventoryFlow.Infrastructure/Persistence/ApplicationDbContext.cs:20-35` is where a `DbSet<WarehouseTransfer>` belongs.
- `.../Configurations/InventoryMovementConfiguration.cs:13-24`: unique `(WorkspaceId, IdempotencyKey)` makes the two transfer movement keys necessarily distinct and private (e.g., `$"{transferId:N}:issue"` and `$"{transferId:N}:receipt"`, 34 chars); never reuse the document/client idempotency key for either movement.
- `.../Configurations/InventoryBalanceConfiguration.cs:39-45`: composite PK `(WorkspaceId, WarehouseId, ProductId)` is the balance row identity; it also has workspace/product index but no source/destination locking abstraction.
- `.../Configurations/SalesFulfillmentConfiguration.cs:58-69` is the document mapping precedent: document unique `(WorkspaceId, IdempotencyKey)`, unique movement FK, newest-first list index, Restrict FKs to source entities and movement. A transfer needs two non-null movement FKs, both individually unique, plus source/destination warehouse FKs (both Restrict), product FK (Restrict), workspace FK (Cascade), precision/key constraints, and `(WorkspaceId, TransferredAtUtc, Id)` history index.
- Existing migrations are EF-generated C# under `backend/src/InventoryFlow.Infrastructure/Persistence/Migrations/`; latest is `20260718135438_AddSalesFulfillments`. Generate a new migration from the modified model and commit its `.cs`, `.Designer.cs`, and updated `ApplicationDbContextModelSnapshot.cs`; do not hand-edit only a migration. Apply command documented in `backend/README.md:10`.

## Required transfer semantics / data contract

### Domain and persistence proposal
Create `WarehouseTransfer` under `Domain/Entities` analogous to `SalesFulfillment`, with immutable private-set fields:
`Id, WorkspaceId, SourceWarehouseId, DestinationWarehouseId, ProductId, Quantity, IdempotencyKey, SourceInventoryMovementId, DestinationInventoryMovementId, TransferredAtUtc`.

Constructor invariants: all IDs nonempty; source != destination; validate quantity/key via `InventoryMovement`; source/destination movement IDs are nonempty and distinct; timestamp UTC. No mutation/reversal endpoint in this slice.

Create `Application/Features/Transfers` models/handlers/validator/interface and `Infrastructure/Transfers/EfWarehouseTransferService`. Suggested HTTP contract:
- `POST /api/transfers`, body `{sourceWarehouseId,destinationWarehouseId,productId,quantity,idempotencyKey}`, optional `Idempotency-Key` header takes precedence; returns `201 Created` and full document response. Replaying identical key returns same transfer/document (existing API convention returns 201 for replay).
- `GET /api/transfers`, returns only current-workspace documents newest-first by `(TransferredAtUtc DESC, Id DESC)`.
- Response contains IDs, quantity, both linked movement IDs, timestamp. Do not expose movement idempotency keys.

### Atomicity, locking, and retry requirements (non-negotiable)
1. Validate DTO/domain quantity and document key before beginning work. Enter `CreateExecutionStrategy().ExecuteAsync` and **create a fresh serializable transaction inside the delegate**, as existing services do. Never reuse an EF transaction across strategy retries and perform no external effects inside the delegate.
2. Within the transaction, look up existing transfer by workspace+document idempotency key **before creating movements**. On a found document, commit/return it. Database unique index remains the final durable replay guard; an execution retry can re-run after unknown commit outcome.
3. Check active product and both active warehouses are workspace-scoped inside the transaction. Any foreign/missing/archived ID returns `null` -> controller 404, with no documents/movements/balances persisted. Reject source==destination as a 400 validation/domain failure.
4. Acquire the two balance identities in one **canonical order**, based on `(WorkspaceId, WarehouseId, ProductId)` (workspace/product equal here, so warehouse GUID ordering). This ordering must be applied to every transfer, including reverse-direction concurrent transfers. It is insufficient to lock source then destination.
5. Missing destination balance is the difficult case: the implementation must serialize/lock the *key/range* before deciding to create it. With SQL Server/EF, a practical explicit helper is a parameterized query in the transaction with `UPDLOCK, HOLDLOCK` (and an order matching the canonical keys), then add missing zero balance(s); ensure the source is loaded/locked too. Avoid interpolating IDs. The existing sequential `SingleOrDefaultAsync` calls at `InventoryLedgerWriter.cs:65-71` do not establish a two-key canonical lock protocol.
6. After both rows are locked/materialized, apply source `-quantity` and destination `+quantity`, then queue exactly two movements with a single shared timestamp: source `Issue` whose `BalanceAfterQuantity` is source post-debit; destination `Receipt` whose `BalanceAfterQuantity` is destination post-credit. Queue one transfer linking those exact IDs, `SaveChangesAsync` once, then commit. Any insufficiency/overflow/constraint/deadlock/transient error must persist neither side.
7. Do not compose the public `EfInventoryLedger.RecordAsync` twice (it opens/commits each transaction). Either add a purpose-built internal pair writer or keep all pair lock/update/movement construction in the transfer service. If reusing/refactoring `InventoryLedgerWriter`, preserve all existing receipt/issue/purchase/sales behavior and make the pair primitive transaction-aware.

## Files likely to change

Backend:
- Add `Domain/Entities/WarehouseTransfer.cs`.
- Add `Application/Features/Transfers/{WarehouseTransferModels.cs,WarehouseTransferHandlers.cs,WarehouseTransferValidators.cs}` (names may follow existing Sales naming) and service interface.
- Add `Infrastructure/Transfers/EfWarehouseTransferService.cs`; potentially extend `Infrastructure/Inventory/InventoryLedgerWriter.cs` only with a correctly deterministic pair operation.
- `Infrastructure/Persistence/ApplicationDbContext.cs`; add `Configurations/WarehouseTransferConfiguration.cs`; add EF migration + designer + snapshot.
- `Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` to register the new service.
- Add `Api/Controllers/WarehouseTransfersController.cs`.

Frontend:
- Add `frontend/src/features/transfers/{transfers-api.ts,transfers-schema.ts,types.ts,pages/TransfersPage.tsx}` mirroring Sales. Use a UUID generated once on submit and retain the exact payload in state on failure; retry must reuse it (`SalesPage.tsx:55-88,164-172` reference).
- `frontend/src/app/router/index.tsx:65-68`: lazy authenticated `/transfers` route.
- `frontend/src/components/layout/Sidebar.tsx:21-33`: navigation entry (reuse a currently installed Hugeicons icon).
- On successful mutation invalidate workspace-scoped transfer history and prefix `['inventory','balances',userId,workspaceId]`, exactly like Sales does at `SalesPage.tsx:62-67`. Load active product/warehouse catalogs and display source → destination/product names with ID fallbacks. Validate source/destination UUIDs and non-equality, product UUID, quantity regex/positive ≤4 decimals.

## Test contract

Add SQL Server integration coverage in `backend/tests/InventoryFlow.IntegrationTests/Api/WarehouseTransferEndpointsTests.cs` using the established fixture/helpers in `SalesFulfillmentEndpointsTests.cs`.

Required cases:
1. POST/GET unauthenticated => 401.
2. Happy path seeded through receipt: source decreases, destination increases (creates destination balance if absent), one transfer, exactly two additional movements of Issue/Receipt, linked IDs match document, movement quantity/timestamp equal, and sum across the two warehouse/product balances is unchanged.
3. Replay same document key returns same transfer and leaves exactly one transfer/two pair movements/balance update. Concurrent same-key requests likewise yield one transfer/pair (all successful replays return 201 per precedent).
4. Insufficient source => 409 and **no** transfer, pair movements, destination balance, or source mutation; destination overflow likewise 400 and no side effects.
5. Foreign source/destination/product and archived source/destination/product => 404, no effects, list isolation by workspace.
6. Same source/destination and invalid quantity/key => 400, no side effects.
7. GET history newest-first with stable ID tiebreaker.
8. Concurrency: simultaneous A→B and B→A transfers (and/or overlapping pair transfers) must finish without deadlock escaping/retrying incorrectly, conserve total stock, create a complete pair per successful document, and leave no negative balance. Because the suite uses real SQL Server Testcontainers, it is the appropriate proof for locking semantics; use enough initial stock and await tasks.
9. Existing ledger/archive tests must still pass; specifically ensure transfer-created on-hand blocks both affected warehouse/product archival via existing behavior.

Frontend has no test harness/scripts beyond typecheck/lint/build in `frontend/package.json`; use build/typecheck/lint as its contract unless tests are introduced.

## Validation commands
```bash
# from repository root
dotnet build backend/InventoryFlow.sln --no-restore
dotnet test backend/tests/InventoryFlow.IntegrationTests/InventoryFlow.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~WarehouseTransferEndpointsTests
dotnet test backend/InventoryFlow.sln --no-restore
dotnet format backend/InventoryFlow.sln --verify-no-changes --no-restore
(cd frontend && npm run typecheck && npm run lint && npm run build)
# migration smoke (requires configured SQL Server)
cd backend && dotnet ef database update --project src/InventoryFlow.Infrastructure --startup-project src/InventoryFlow.Api
```

## Review findings / residual risks
- **HIGH — `InventoryLedgerWriter.cs:65-76`:** one-row lookup/update cannot safely implement a transfer pair. Calling it twice introduces independent balance-lock order, may commit a half-transfer if public ledger APIs are composed, and does not protect a missing destination identity. Implement a pair-level deterministic lock protocol.
- **HIGH — `InventoryMovementConfiguration.cs:20`:** movement idempotency is globally unique within workspace. Reusing the caller’s transfer key for both legs will violate the unique index. Use two document-derived private keys and use the transfer table’s own unique workspace/key constraint for replay.
- **MEDIUM — `EfSalesFulfillmentService.cs:18-45`:** its serializable/retry template is necessary but insufficient for two rows; naïvely copying it and querying source/destination in request order allows reverse transfers to deadlock and leaves missing-row races.
- **MEDIUM — migration/snapshot:** the model snapshot and generated designer migration are essential to future EF migrations; a table-only migration or missing unique indexes weakens idempotency/audit guarantees.
- **Residual:** SQL Server locking hints/raw SQL should be isolated, parameterized, and proved by real SQL Server integration concurrency tests. EF in-memory/sqlite behavior would not be evidence for SQL Server range locks. Querying with locks needs tracking entities (not `AsNoTracking`) for balance mutation.

## Meta-prompt handoff (compact contract)
**Goal:** Implement the minimal authenticated `/api/transfers` + `/transfers` vertical slice described above: one immutable transfer document, paired existing Issue/Receipt ledger entries, two balances, durable document replay, deterministic two-key locking, migration, UI, and real-SQL-Server integration tests.

**Evidence/constraints:** Follow Sales/Purchases document shape and `CreateExecutionStrategy`/serializable pattern, but do not reuse their single-row writer logic as the pair transaction. Keep existing `InventoryMovementType` values; no new movement enum. Source/destination/product/workspace checks are server-side and active-only. Header idempotency overrides body. Source != destination. Pair must be all-or-nothing, conservation-preserving, and linked by immutable document FKs. Add generated migration/designer/snapshot. No edits beyond this slice and no commits.

**Success criteria:** A replay returns original transfer; exactly two distinct linked movements are persisted for a successful document; both balance post-values are correct; no partial side effects for rejected/failed transfer; opposite-direction concurrent transfers use canonical key ordering and preserve totals; frontend retry retains key and successful transfer refreshes balances/history; full targeted tests and static checks pass.

**Suggested approach:** Introduce transfer entity/service/model/controller/configuration first. In the transfer service, perform document replay lookup, lifecycle validation, canonical pair key/range locks, balance creation/load, paired mutation/movement construction, document save, commit in one retry delegate. Then migration, integration tests, frontend mirror of Sales, route/nav.

**Stop/escalation:** Escalate before changing movement enum, accepting cross-workspace workspace ID, adding multi-line/approval/reversal behavior, or if SQL Server range locking cannot be implemented/tested with the available provider. Stop once the bounded contract and validation pass; do not broaden into general inventory refactoring.

**Resolved assumptions:** This is an immediate posted, one-product, one-quantity transfer; transfer does not create a new ledger movement type; destination balance may be zero/missing and must be created atomically; existing 201-on-replay convention is retained.