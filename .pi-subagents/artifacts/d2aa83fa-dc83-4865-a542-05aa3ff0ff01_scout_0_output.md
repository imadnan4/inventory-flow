# Code Context

## Files Retrieved
1. `backend/src/InventoryFlow.Infrastructure/Inventory/EfInventoryLedger.cs` (lines 13-68) — current receipt/issue implementation; serializable transaction, workspace source checks, idempotency, balance mutation, movement insert, and one `SaveChanges`.
2. `backend/src/InventoryFlow.Application/Features/Inventory/IInventoryLedger.cs` (lines 5-14) — ledger abstraction available to a receiving feature.
3. `backend/src/InventoryFlow.Domain/Entities/InventoryMovement.cs` (lines 6-76) — only `Receipt`/`Issue` movement types, precision/positivity/idempotency constraints; no business-source reference.
4. `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/InventoryMovementConfiguration.cs` (lines 13-24) — unique `(WorkspaceId, IdempotencyKey)` and only workspace/warehouse/product foreign keys.
5. `backend/src/InventoryFlow.Application/Features/Inventory/InventoryMovementCommands.cs` (lines 7-32) and `InventoryHandlers.cs` (lines 7-39) — current generic receipt command maps straight to the ledger.
6. `backend/src/InventoryFlow.Api/Controllers/InventoryController.cs` (lines 9-56) — authenticated, current-workspace-scoped generic receipt endpoint is already public.
7. `backend/src/InventoryFlow.Infrastructure/Persistence/ApplicationDbContext.cs` (lines 10-47) — DbContext extension point for PO/line/receipt sets.
8. `backend/src/InventoryFlow.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` (lines 74-87) — catalog and ledger registrations; add a receiving application port/EF implementation here.
9. `backend/src/InventoryFlow.Application/Features/Suppliers/ISupplierCatalog.cs` (lines 5-21), `backend/src/InventoryFlow.Domain/Entities/Supplier.cs` (lines 6-61), and `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/SupplierConfiguration.cs` (lines 7-21) — supplier is workspace-scoped/archivable, has no PO relationships, and can be a required active source at PO creation.
10. `backend/tests/InventoryFlow.IntegrationTests/Api/InventoryEndpointsTests.cs` (lines 29-198) — established SQL-backed API test fixture and proof expectations for ledger idempotency, overdraft, precision, archive, and concurrency behavior.
11. `frontend/src/app/router/index.tsx` (lines 36-75) — `/purchases` is currently an authenticated placeholder.
12. `frontend/src/features/inventory/pages/InventoryPage.tsx` (lines 57-124, 135-220) — existing TanStack Query, catalog selectors, validation/mutation/error/retry pattern to reuse.
13. `frontend/src/features/inventory/inventory-api.ts` (lines 1-26) — generic receipt client sends an idempotency header and body key.

## Key Code

### Exact prerequisites
* Suppliers, products, warehouses, workspace tenancy, EF migrations, and the inventory ledger are present. PO work must require an **active supplier**, active product(s), and an active destination warehouse, all belonging to the server-resolved workspace.
* A PO must snapshot line description/SKU and ordered unit price if commercial history is desired; the present `Product` and `Supplier` entities are mutable/archiveable catalog records, so references alone do not preserve document history.
* Define scope before coding: smallest safe slice is a single destination warehouse per PO, decimal quantity at four places, one supplier, multiple lines, partial receipts; exclude approval, tax/currency totals, supplier-product pricing, returns, cancellation-after-receipt, attachments, and receiving into multiple warehouses.

### Recommended persistence and lifecycle
Add domain entities/configurations/migration for:
* `PurchaseOrder`: `Id`, `WorkspaceId`, `SupplierId`, `WarehouseId`, document number, `Draft|Ordered|PartiallyReceived|Received|Cancelled`, timestamps (and optional `OrderedAtUtc`).
* `PurchaseOrderLine`: PO FK, `ProductId`, ordered quantity, received quantity, sequence, product snapshot fields. Unique `(PurchaseOrderId, LineNumber)`.
* `PurchaseReceipt` plus `PurchaseReceiptLine` **or** a receipt-operation id/key persisted on lines. The former gives a durable receiving audit. Each receipt line stores PO line, quantity, warehouse/product snapshot/reference, recorded movement ID, and a receipt idempotency key. Add a unique workspace-scoped receipt-operation key (or document/line key) that matches retry semantics.

Minimal lifecycle: create draft with at least one valid positive line -> submit/order (locks supplier/warehouse/line ordered quantities) -> receive selected positive remaining quantities, transitioning Ordered → PartiallyReceived → Received -> cancel only Draft/Ordered with no receipt. Do not permit changes to supplier, warehouse, ordered quantities, or product lines after any receipt; the first slice can omit draft editing entirely by creating the PO as Ordered.

### Atomic ledger integration (blocker if omitted)
`EfInventoryLedger.RecordAsync` owns a serializable transaction and calls `SaveChangesAsync` itself (`EfInventoryLedger.cs:20-56`). A PO receiving handler cannot safely update PO receipt quantities/status before/after invoking it: a failure creates either stock without receipt state or receipt state without stock.

Create a receiving application port (for example `IPurchaseOrderReceivingService.ReceiveAsync`) whose EF implementation owns **one** execution-strategy retry and serializable transaction. In that transaction:
1. Lookup PO, supplier, warehouse, products, and lines under the current workspace; reject archived/missing references, invalid lifecycle, duplicate receipt key, zero/over-receipt.
2. For every receipt line, load/create balance, apply `+quantity`, add an immutable `InventoryMovement(Receipt, ...)`, and add the receipt-line link to that movement.
3. Increment each PO line received quantity; derive PO status; write receipt header/lines and all ledger movements/balances in one `SaveChangesAsync`; commit.
4. On exact retry key, return the original receipt/result without duplicating movements or quantities.

Refactor the ledger internals into a transaction-aware shared writer, or keep PO receiving’s ledger mutation in its own infrastructure service. Do **not** call public `IInventoryLedger.RecordAsync` once per PO line: each call commits independently and cannot be composed with PO persistence. Also generate a distinct deterministic key per receipt line if the ledger unique key remains `(WorkspaceId, IdempotencyKey)` (`InventoryMovementConfiguration.cs:20`); one receipt-level key cannot be used for several movement rows.

### API/UI acceptance
Backend new feature files should follow `Features/Suppliers`/`Features/Inventory`: requests, models, validators, MediatR handlers, controller, persistence port/EF implementation, DI registration, DbContext sets/configurations, and generated migration. Suggested minimum endpoints:
* `POST /api/purchase-orders` (create ordered PO), `GET /api/purchase-orders`, `GET /api/purchase-orders/{id}`.
* `POST /api/purchase-orders/{id}/receipts` with receipt key in `Idempotency-Key` (header preferred) and `{ lines: [{ purchaseOrderLineId, quantity }] }`; return receipt, line totals, PO status, and movement IDs.
* Optional only if UI exposes them: `POST .../submit`, `POST .../cancel`; otherwise create-as-ordered avoids UI-only lifecycle controls.

Frontend: replace the authenticated `/purchases` placeholder at `frontend/src/app/router/index.tsx:65` with `features/purchases/pages/PurchaseOrdersPage.tsx`; implement API/types/schema. Reuse inventory page query-key workspace partitioning and retry behavior (`InventoryPage.tsx:57-124`). UI acceptance: user can choose active supplier/warehouse/products, enter valid positive quantities, create/list/open ordered PO, receive a subset of remaining lines once, see received/remaining/status, retry the same failed/in-doubt receive with the same key, and observe inventory on-hand increase exactly once. Purchases must not call the generic `/api/inventory/receipts` endpoint directly, because it has no PO linkage (`InventoryController.cs:15-28`).

### Test acceptance
Extend integration tests beside `InventoryEndpointsTests.cs` to prove: workspace isolation; inactive/nonexistent supplier/product/warehouse rejection; ordered quantities/precision; status transitions; partial + final receipt; over-receipt rejection; duplicate same receipt key returns original response with one movement per received line; transaction rollback leaves neither movements/balances nor receipt totals on a validation/persistence failure; two concurrent receipts cannot exceed ordered remainder; and generic inventory receipt remains unaffected. Add domain unit tests for PO/line transitions and receipt constraints. Run backend test suite and frontend lint/typecheck/build after implementation.

## Architecture

Authenticated controllers resolve `ICurrentWorkspace`, send MediatR commands, application handlers depend on narrow ports, and EF infrastructure persists workspace-scoped entities. The current ledger materializes balances and immutable movements atomically only for a standalone generic movement. Purchases should follow the same boundaries but use a dedicated receiving transaction that writes PO state and ledger state together. The frontend is React Router + TanStack Query; existing suppliers/products/warehouses provide selectors, while Inventory remains the read-back verification surface.

## Review Findings

* **blocker — `backend/src/InventoryFlow.Infrastructure/Inventory/EfInventoryLedger.cs:20-56`:** standalone transaction/commit prevents atomic PO receipt-state plus ledger persistence if reused directly.
* **high — `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/InventoryMovementConfiguration.cs:20-24`:** movements have no purchase-order/receipt foreign key or source reference, so receiving cannot be auditable/reconcilable without new receipt-link persistence.
* **high — `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/InventoryMovementConfiguration.cs:20`:** idempotency is unique per workspace movement, not receipt operation; multi-line receipt must not share its single client key across rows.
* **medium — `backend/src/InventoryFlow.Api/Controllers/InventoryController.cs:15-28`:** generic receipt can add stock outside PO controls; purchase UI/API must use its own receipt endpoint and business policy must decide whether generic receipts stay permitted.
* **medium — `frontend/src/app/router/index.tsx:65`:** purchase navigation exists but is a placeholder; no current UI/API/type/test footprint exists for POs.

## Start Here

Open `backend/src/InventoryFlow.Infrastructure/Inventory/EfInventoryLedger.cs` first. Its transaction boundary decides the receiving service design; settle a composable, single-transaction write path before defining PO endpoints or UI.