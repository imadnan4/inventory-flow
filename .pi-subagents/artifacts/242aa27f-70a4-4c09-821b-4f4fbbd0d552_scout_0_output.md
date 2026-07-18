# Code Context

## Files Retrieved
1. `backend/src/InventoryFlow.Infrastructure/Inventory/InventoryLedgerWriter.cs` (lines 1-40) - shared writer validates active tenant-scoped sources, alters one balance, and appends one movement.
2. `backend/src/InventoryFlow.Infrastructure/Purchases/EfPurchaseReceiptService.cs` (lines 1-60) - closest receipt transaction/idempotency pattern.
3. `backend/src/InventoryFlow.Infrastructure/Sales/EfSalesFulfillmentService.cs` (lines 1-54) - closest issue transaction/idempotency pattern, including insufficient-stock rollback.
4. `backend/src/InventoryFlow.Domain/Entities/InventoryMovement.cs` (lines 1-75) and `InventoryBalance.cs` (lines 1-58) - movement direction, numeric/idempotency constraints, and nonnegative balance invariant.
5. `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/InventoryMovementConfiguration.cs` (lines 1-25) and `InventoryBalanceConfiguration.cs` (lines 1-21) - current unique/idempotency and balance keys.
6. `backend/src/InventoryFlow.Infrastructure/Warehouses/EfWarehouseCatalog.cs` (lines 35-72) and `Products/EfProductCatalog.cs` (lines 37-70) - serializable archival and nonzero-on-hand protections.
7. `backend/src/InventoryFlow.Api/Controllers/PurchaseReceiptsController.cs` (lines 1-39), `SalesFulfillmentsController.cs` (lines 1-39), and `InventoryController.cs` (lines 1-61) - established authenticated controller/API shapes.
8. `backend/src/InventoryFlow.Application/Features/Purchases/PurchaseReceiptModels.cs` (lines 1-27), `Sales/SalesFulfillmentModels.cs` (lines 1-25), and `Inventory/InventoryMovementCommands.cs` (lines 1-31) - request/command/response/service conventions.
9. `backend/src/InventoryFlow.Infrastructure/Persistence/ApplicationDbContext.cs` (lines 1-48) and `DependencyInjection/InfrastructureServiceCollectionExtensions.cs` (lines 1-104) - required DbSet and service registration points.
10. `backend/tests/InventoryFlow.IntegrationTests/Api/PurchaseReceiptEndpointsTests.cs` (lines 29-221) and `SalesFulfillmentEndpointsTests.cs` (lines 29-210) - reusable API test coverage patterns for auth, tenant isolation, archives, replay, atomicity, overdraft, ordering, and concurrent replay.
11. `frontend/src/features/purchases/pages/PurchasesPage.tsx` (lines 1-115), `purchases-api.ts` (lines 1-17), `purchases-schema.ts` (lines 1-16), and `types.ts` (lines 1-17) - closest UI/API/idempotent-retry slice.
12. `frontend/src/app/router/index.tsx` (lines 35-77) and `frontend/src/components/layout/Sidebar.tsx` (lines 1-113) - route and navigation insertion points. No frontend test files exist.

## Key Code

**Existing safety building blocks.** Receipt/fulfillment services start an EF execution strategy and `Serializable` transaction, look up their document by `(WorkspaceId, IdempotencyKey)`, then call the shared writer, save both document and movement/balance updates, and commit. The writer only accepts an active warehouse/product in the supplied workspace and checks movement idempotency by `(WorkspaceId, IdempotencyKey)`. `InventoryBalance.Apply` rejects negative and overflowed values.

**Smallest safe transfer design.** Add a standalone `Transfers` vertical slice rather than overloading purchases/sales or exposing a client-composed issue+receipt:

- Domain entity `WarehouseTransfer`: `Id, WorkspaceId, SourceWarehouseId, DestinationWarehouseId, ProductId, Quantity, IdempotencyKey, SourceInventoryMovementId, DestinationInventoryMovementId, TransferredAtUtc`; immutable, rejects empty IDs, `source == destination`, invalid quantity/key/time. Use two explicit movement references so history/audit proves both legs.
- Extend `InventoryMovementType` with `TransferOut` and `TransferIn` (or retain `Issue`/`Receipt` only if document links make provenance sufficient). **Do not reuse one movement idempotency key:** movement uniqueness is workspace-wide, so derive internal keys from a transfer id, e.g. `transferId:N:out` and `transferId:N:in`; the client key belongs only to the transfer document.
- Add `DbSet<WarehouseTransfer>`, configuration: unique `(WorkspaceId, IdempotencyKey)`, unique each movement FK, history index `(WorkspaceId, TransferredAtUtc, Id)`, workspace FK cascade and warehouse/product/movement FKs restrict. Add migration/snapshot.
- `EfWarehouseTransferService.RecordAsync`: validate/normalize before a serializable transaction; first replay lookup by workspace/key; verify **both** warehouse IDs and product are active and workspace-scoped; issue the source then receipt the destination through a transfer-aware writer; add document; perform one `SaveChanges`/commit. Any missing/archive source, insufficient source balance, destination overflow, or persistence failure must roll back both legs and document.
- Avoid calling the public `IInventoryLedger.RecordAsync` twice: each call owns/commits its own transaction and therefore cannot provide two-balance atomicity. Reuse/refactor the internal writer within the one transfer transaction instead.

**API/UI.** Mirror `/api/purchases/receipts`: `POST /api/inventory/transfers` (Idempotency-Key header overrides payload key) and `GET /api/inventory/transfers`, server-resolve workspace and return 404 for invalid/archived/foreign sources; 409 for insufficient inventory. A dedicated frontend `features/transfers` page is the narrowest UI: source and destination warehouse selects, product, quantity, `crypto.randomUUID()` at initial submission, preserve payload for retry, invalidate transfer history and `['inventory','balances',userId,workspaceId]` on success. Add `/transfers` lazy route and sidebar item. Reusing `PurchasesPage` would incorrectly carry supplier semantics.

## Architecture

Browser payload -> authenticated controller -> workspace derived from JWT membership (`ICurrentWorkspace`) -> MediatR command/handler -> transfer service -> one serializable SQL transaction -> two `InventoryBalances` plus two immutable `InventoryMovements` plus one transfer document. Reads are always filtered by workspace. The existing warehouse/product archive paths use serializable transactions and reject any nonzero balance, so a successful transfer leaves the destination warehouse non-archivable until stock is removed; source becomes archivable only if all its balances reach zero.

## Review Findings

1. **blocker — `InventoryLedgerWriter.cs:18-37`:** invoking the current writer twice with the same client idempotency key makes the second leg return the first movement because `InventoryMovements` enforces unique `(WorkspaceId, IdempotencyKey)` (`InventoryMovementConfiguration.cs:17`). Transfer needs a transfer-level replay key and two distinct internal leg keys.
2. **blocker — `EfInventoryLedger.cs:13-27`:** composing existing public receipt/issue calls is not atomic: each starts and commits its own serializable transaction. A failure on the destination can leave stock removed from source.
3. **high — `InventoryLedgerWriter.cs:21-35`:** it maps every non-`Receipt` movement to a negative delta. Extending the enum requires explicit direction mapping (`Receipt`/`TransferIn` positive; `Issue`/`TransferOut` negative), otherwise inbound transfer stock is decremented.
4. **high — `EfWarehouseCatalog.cs:41-66`, `EfProductCatalog.cs:39-64`:** transfer must validate both active warehouses and product *inside its same serializable transaction*; validation outside allows archives between validation and posting. Existing writer validates only one warehouse.
5. **medium — `PurchaseReceiptEndpointsTests.cs:29-221` and `SalesFulfillmentEndpointsTests.cs:29-210`:** strong analogous tests exist, but no transfer coverage or frontend test framework/files currently exist.

## Residual Risks

- Opposite-direction concurrent transfers can deadlock when each transaction locks balances in source-then-destination order. Query/create both balances in deterministic warehouse-ID order before applying source/destination deltas (and retain EF execution strategy retries).
- The current archive tests only cover one-balance receipt races. Add transfer-vs-source-archive and transfer-vs-destination-archive concurrency tests; permitted results should preserve the invariant (transfer created with both active, or archive wins and transfer is 404) without one-sided movements.
- A transfer document’s idempotency response currently has no request-payload fingerprint. Like receipts/fulfillments, same key with changed payload returns original document; preserve this convention or explicitly introduce conflict semantics across all document APIs.

## Start Here

Open `backend/src/InventoryFlow.Infrastructure/Inventory/InventoryLedgerWriter.cs` first. It is the critical reuse boundary: factor it only enough to write two uniquely keyed legs within the transfer service’s single serializable transaction, while retaining its active-source and balance invariants.