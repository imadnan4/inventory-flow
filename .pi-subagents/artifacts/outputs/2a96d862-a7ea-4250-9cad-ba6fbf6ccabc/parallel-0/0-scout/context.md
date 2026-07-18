# Code Context

## Files Retrieved
1. `backend/src/InventoryFlow.Domain/Entities/Product.cs` (lines 6-60) — existing workspace-owned, archived product root; movement must reference active product IDs.
2. `backend/src/InventoryFlow.Domain/Entities/Warehouse.cs` (line 4) — only physical stock destination currently exists; there is no bin/location model.
3. `backend/src/InventoryFlow.Infrastructure/Persistence/ApplicationDbContext.cs` (lines 14-44) — persistence seam has products and warehouses, but no balance or ledger sets.
4. `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/ProductConfiguration.cs` (lines 13-20) and `WarehouseConfiguration.cs` (line 4) — workspace FKs/index conventions to reproduce for new inventory tables.
5. `backend/src/InventoryFlow.Api/Controllers/ProductsController.cs` (lines 9-48) and `WarehousesController.cs` (line 6) — authenticated controller/workspace-resolution/MediatR HTTP pattern.
6. `backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs` (lines 10-23) — trusted workspace source; it fails closed unless the caller has exactly one Owner membership.
7. `backend/src/InventoryFlow.Infrastructure/{Products/EfProductCatalog.cs,Warehouses/EfWarehouseCatalog.cs}` (lines 9-37; line 6) — repositories manually scope every read by `WorkspaceId`; neither provides transactional stock mutation.
8. `backend/src/InventoryFlow.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` (lines 38-82) and `backend/src/InventoryFlow.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs` (lines 12-28) — register a new infrastructure port here; handlers/validators are assembly scanned.
9. `backend/src/InventoryFlow.Api/ExceptionHandling/GlobalExceptionHandler.cs` (lines 25-77) — add stable mappings for an idempotency or concurrency conflict rather than leak a database error.
10. `frontend/src/app/router/index.tsx` (lines 36-68) — `/inventory` is a protected placeholder, the exact UI replacement point.
11. `frontend/src/features/{products/pages/ProductsPage.tsx,warehouses/pages/WarehousesPage.tsx}` (products lines 15-150; warehouses lines 15-150) — React Query/query-key/form/error conventions; warehouse page has an unrelated duplicate `name` input at lines 129-136.
12. `frontend/src/lib/api-client.ts` (lines 7-57) and `frontend/src/features/auth/auth-store.ts` (lines 12-34) — required API client and workspace-aware cache lifecycle.
13. `backend/tests/InventoryFlow.IntegrationTests/Api/{ProductEndpointsTests.cs,WarehouseEndpointsTests.cs}` (product lines 27-107; warehouse lines 27-115) and `AuthenticatedApiFixture.cs` (lines 15-91) — real SQL Server migration, authentication, tenancy, and concurrency test harness.

## Key Code

### Smallest safe vertical slice: idempotent **stock receipt** into a warehouse
Do **not** implement transfers, issues, generic signed adjustments, editing/deleting movements, locations, or dashboard metrics. The current prerequisites are now satisfied: a workspace-scoped active `Product` and `Warehouse` exist. The smallest movement that cannot make stock negative is a positive receipt. It establishes the immutable audit ledger and current per-warehouse balance needed for every later movement.

**New domain/persistence model**
- `InventoryMovement`: immutable `Id`, `WorkspaceId`, `ProductId`, `WarehouseId`, positive `Quantity` (`decimal`, fixed scale e.g. 18,4), server `OccurredAtUtc`, server `CreatedAtUtc`, `Type = Receipt`, and required normalized client idempotency key (max length explicitly chosen, e.g. 100). Do not accept workspace/user/timestamps/balance from the browser.
- `InventoryBalance`: `WorkspaceId`, `ProductId`, `WarehouseId`, non-negative `Quantity`, plus a SQL Server `rowversion`/concurrency token. Its unique key is `(WorkspaceId, ProductId, WarehouseId)`.
- Both rows must FK to `Workspace`; movements also FK to `Product` and `Warehouse`. Product/warehouse IDs must be validated as **active and in the resolved workspace** before writing. The ledger is append-only; balance is a derived projection, never the audit record.

**Atomic command**
`POST /api/inventory-movements` accepts only:
```json
{ "productId": "guid", "warehouseId": "guid", "quantity": 12.5, "idempotencyKey": "client-generated-key" }
```
Return `201 Created` on first submission and the existing movement response (`id`, product/warehouse summaries or IDs, quantity, `occurredAtUtc`) on a same-workspace same-key retry (`200 OK` is simplest). The implementation must run: scoped active-root validation → conditional/additive balance update → ledger insert → idempotency uniqueness handling in **one database transaction**. If a duplicate-key race occurs, discard/reload the stored movement by `(WorkspaceId, IdempotencyKey)` and return it; never increment twice.

Avoid EF read-modify-write for balances: concurrent receipts otherwise lose increments. Use one atomic SQL Server update (`Quantity = Quantity + @quantity`, then insert if no row) under a transaction, or serializable/key-range locking with retry; constrain balance quantity non-negative even though receipt input is positive. EF retry-on-failure is configured, so a transaction must use EF's execution strategy where required. The unique idempotency index is the durable protection against browser/network retries.

Minimum read contract: `GET /api/inventory-movements?limit=...` returns only this workspace's receipts, deterministic `OccurredAtUtc DESC, Id DESC`, and preferably current `balanceQuantity` per row or a separate `GET /api/inventory-balances` listing product/warehouse/current quantity. Keep the first UI to receipt form plus recent-receipt list; no speculative “on hand” dashboard integration.

**Exact implementation surface**
- New: `backend/src/InventoryFlow.Domain/Entities/InventoryMovement.cs`, `InventoryBalance.cs`.
- New: `backend/src/InventoryFlow.Application/Features/Inventory/{InventoryModels.cs,InventoryHandlers.cs,InventoryValidators.cs,IInventoryLedger.cs}`. Validators require non-empty IDs/key and finite positive quantity with max scale; domain repeats invariant checks.
- New: `backend/src/InventoryFlow.Infrastructure/Inventory/EfInventoryLedger.cs` and configurations `Persistence/Configurations/{InventoryMovementConfiguration.cs,InventoryBalanceConfiguration.cs}`. Register `IInventoryLedger` in `InfrastructureServiceCollectionExtensions.cs` alongside product/warehouse registrations.
- Modify: `ApplicationDbContext.cs` to add two DbSets; create an additive EF migration and update `ApplicationDbContextModelSnapshot.cs`.
- New: `backend/src/InventoryFlow.Api/Controllers/InventoryMovementsController.cs`; copy `ProductsController.cs:19-47`: `[Authorize]`, resolve `ICurrentWorkspace`, `Forbid()` on null, pass only `workspace.Id` to MediatR.
- Modify: `GlobalExceptionHandler.cs` for explicit 409/possibly 422 mapping if a known persistence conflict cannot be replayed safely.
- New frontend: `frontend/src/features/inventory/{types.ts,inventory-api.ts,inventory-schema.ts,pages/InventoryPage.tsx}`; modify router line 53 to lazy-load it. `inventory-api.ts` must use `apiClient`, not raw axios. Query keys must include `user.id` and `user.workspace.id`, as demonstrated at `WarehousesPage.tsx:15-40`.

### Preconditions and API/UI contract
1. Both a non-archived product and warehouse must already exist in the caller’s workspace. Archived or foreign IDs return `404` without disclosing existence.
2. `POST` unauthenticated is `401`; valid JWT but no uniquely resolvable owner workspace is `403` (current resolver behavior).
3. Quantity must be server-validated positive fixed-point decimal; prohibit zero, negative, excessive precision/range, NaN/infinity, and client-selected occurrence time in this first slice.
4. Same idempotency key with changed payload must return a stable conflict (`409`), not silently treat it as the old receipt. Key uniqueness is workspace-scoped, so another workspace may reuse it.
5. UI loads active products and warehouses for selects, labels both inputs, has a numeric quantity field and generated/persisted retry key, disables submit while pending, reports RFC 7807 errors, and invalidates/refetches receipt/balance data on success. It must preserve submitted state on retry/error.

## Architecture
Browser `InventoryPage` → feature `inventory-api.ts` → `apiClient` bearer/refresh handling → protected `InventoryMovementsController` → server-only `ICurrentWorkspace` → MediatR command/query → `IInventoryLedger` → SQL Server transaction containing immutable ledger plus balance projection. Existing product/warehouse repositories show that tenancy is not global: every new lookup/query and both unique indexes must explicitly include `WorkspaceId`.

The warehouse itself is the stock location for this deliberately narrow receipt slice. Add bin/location only before transfers or detailed placement; adding it now would expand the operation without addressing receipt correctness.

## Review Findings / Residual Risks
- **High — no existing inventory persistence:** `ApplicationDbContext.cs:21-31` has only catalog roots. A UI-only movement or a mutable `Product.OnHand` field has no immutable audit trail and is unsafe.
- **High — lost-update/double-submit risk:** `EfProductCatalog.cs:13-24` and `EfWarehouseCatalog.cs:6` only use ordinary EF `SaveChanges`; copying this pattern for read/increment/write loses concurrent receipts. Require atomic additive update, transaction, idempotency index, and concurrency tests.
- **High — tenancy is manual:** `CurrentWorkspaceResolver.cs:18-22` resolves one owner workspace, while product queries manually filter at `EfProductCatalog.cs:28-34`. A missing predicate in balance/movement queries or root validation creates horizontal data exposure.
- **Medium — SQL uniqueness alone is insufficient for idempotency:** the product/warehouse unique-conflict approach turns a duplicate into 409. Receipt retries must instead retrieve the original committed movement; otherwise clients may retry with a new key and duplicate stock.
- **Medium — archive behavior is currently unsafe to extend blindly:** warehouse archive currently does not check inventory references (`WarehousesController.cs` line 6; `WarehouseEndpointsTests.cs:47-52`). Once inventory exists, archive must be prohibited when balance is nonzero (or a later explicit close/transfer workflow must drain it). This is a required follow-up policy/change, not part of receipt UI.
- **Low — existing warehouse form defect:** `WarehousesPage.tsx:129-136` duplicates `name`; unrelated to movement but should not be copied into the new form.

## Test Requirements
- **Domain unit tests:** reject empty IDs, non-UTC server times, non-positive/out-of-scale quantity, invalid key; prove receipt/movement is immutable and balance cannot become negative.
- **SQL-backed integration tests** (new `backend/tests/InventoryFlow.IntegrationTests/Api/InventoryMovementEndpointsTests.cs`, use `AuthenticatedApiFixture`): unauthenticated 401; create receipt and list/balance happy path; invalid input ProblemDetails; foreign/archived product or warehouse 404; workspace isolation; same idempotency key replay returns same movement and one ledger/balance increment; same key/different body conflict; simultaneous identical-key requests create one movement; simultaneous different-key receipts produce exact summed balance; migration applies.
- **Persistence-focused test:** prove movement insert and balance update roll back together on failure (no ledger without projection or projection without ledger).
- **Frontend:** no frontend test harness is present. At minimum test form validation/loading/error/retry manually and run `bun run typecheck`, `bun run lint`, `bun run build`; run `dotnet build backend/InventoryFlow.sln`, `dotnet test backend/InventoryFlow.sln`, and `dotnet format backend/InventoryFlow.sln --verify-no-changes --no-restore`.

## Start Here
Open `backend/src/InventoryFlow.Infrastructure/Persistence/ApplicationDbContext.cs` first: it confirms the missing ledger/balance boundary, the migration target, and all existing workspace-scoped roots the receipt transaction must validate.

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "Concrete minimal receipt slice, exact files/line ranges, API/UI contract, tenancy/concurrency findings, and test requirements are documented."
    }
  ],
  "changedFiles": [],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "git status --short && git log --oneline -5 && targeted nl/read/grep inspection",
      "result": "passed",
      "summary": "Confirmed HEAD 4eaac6e warehouse slice and inspected current source/test seams; no source changes made."
    }
  ],
  "validationOutput": [
    "Read-only scouting task; build and test suites were not run.",
    "Findings written to the required artifact path."
  ],
  "residualRisks": [
    "A future issue/transfer needs an explicit negative-stock, locking, and reversal policy; it is intentionally out of this receipt slice.",
    "Warehouse archive must gain a nonzero-balance guard once receipts exist.",
    "Only Owner membership is resolvable; shared-workspace role authorization remains unimplemented."
  ],
  "noStagedFiles": true,
  "diffSummary": "No repository source edits; wrote scouting artifact only.",
  "reviewFindings": [
    "high: backend/src/InventoryFlow.Infrastructure/Persistence/ApplicationDbContext.cs:21-31 - ledger and balance persistence do not exist.",
    "high: backend/src/InventoryFlow.Infrastructure/{Products/EfProductCatalog.cs:13-24,Warehouses/EfWarehouseCatalog.cs:6} - ordinary SaveChanges pattern is unsafe for concurrent additive stock updates.",
    "high: backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs:18-22 - tenant scope is manually propagated and must be applied to every inventory lookup/mutation.",
    "medium: backend/src/InventoryFlow.Api/Controllers/WarehousesController.cs:6 - archive has no inventory-aware guard; add policy once balances exist.",
    "low: frontend/src/features/warehouses/pages/WarehousesPage.tsx:129-136 - duplicate name control is an existing unrelated UI defect."
  ],
  "manualNotes": "Recommended scope is an idempotent positive stock receipt into an existing warehouse, not a generic adjustment or transfer."
}
```