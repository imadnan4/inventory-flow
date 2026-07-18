# Code Context

## Files Retrieved
1. `README.md` (lines 3-14, 139-139) — product scope includes warehouses/stock; roadmap names authorization, catalog, then inventory movements but does not prescribe the granular order.
2. `backend/src/InventoryFlow.Domain/Entities/Product.cs` (lines 7-60) — established pattern for a workspace-owned aggregate: server-owned `WorkspaceId`, normalized business fields, UTC timestamp, and archival.
3. `backend/src/InventoryFlow.Application/Features/Products/ProductModels.cs` (lines 6-19) and `ProductHandlers.cs` (lines 7-34) — command/query/response and MediatR patterns to mirror for warehouse/location use cases.
4. `backend/src/InventoryFlow.Api/Controllers/ProductsController.cs` (lines 9-48) — protected controller resolves workspace on the server before dispatch; exact HTTP integration pattern.
5. `backend/src/InventoryFlow.Infrastructure/Persistence/ApplicationDbContext.cs` (lines 14-43) and `Configurations/ProductConfiguration.cs` (lines 8-21) — add `DbSet`s/configurations here; discovery-based configuration means no context mapping edit beyond DbSets.
6. `backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs` (lines 10-23) — current tenant source is authenticated owner membership and deliberately fails closed if it finds anything other than one owner workspace.
7. `backend/src/InventoryFlow.Domain/Entities/WorkspaceMember.cs` (lines 6-28) and `WorkspaceMemberRole.cs` (lines 3-8) — membership is immutable and only permits `Owner`; permissions are not yet representable.
8. `backend/src/InventoryFlow.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` (lines 38-105) — exact DI seam for warehouse repository/catalog registrations; authentication and `ICurrentWorkspace` are already registered.
9. `frontend/src/app/router/index.tsx` (lines 36-60), `frontend/src/components/layout/Sidebar.tsx` (lines 23-35) — `/warehouses` is already an authenticated, visible placeholder route.
10. `frontend/src/features/products/pages/ProductsPage.tsx` (lines 15-150), `products-api.ts` (lines 4-11), `products-schema.ts` (lines 3-8) — reusable React Query, Zod, API-client, loading/error, archive UI conventions.
11. `backend/tests/InventoryFlow.IntegrationTests/Api/ProductEndpointsTests.cs` (lines 27-88) — SQL Server-backed API pattern proves canonicalization, lifecycle, isolation, and uniqueness/concurrency.

## Key Code

**Prioritized recommendation: P0 — workspace-scoped warehouse + location foundation.** Deliver a single authenticated **Warehouses** page that creates a warehouse and its required first location in one transaction, lists active warehouses with active locations, and archives a warehouse/location only while no future inventory records exist (currently always permitted, but encode the lifecycle method rather than delete). Do **not** add quantities, adjustments, transfers, valuation, purchase/sales linkage, or dashboard metrics.

This is the smallest high-value slice after products: it turns an already-visible `/warehouses` navigation item into durable operational structure and establishes the physical ownership boundary that an inventory ledger needs. Make `Location` a child of `Warehouse` and store `WorkspaceId` on both aggregates. That permits every later movement to verify product, source location, and destination location are in the same trusted workspace without a tenant-crossing join.

Suggested minimal fields/invariants:
- `Warehouse`: `Id`, `WorkspaceId`, normalized required `Name` (and optionally immutable normalized `Code` only if users need a compact identifier), `CreatedAtUtc`, `ArchivedAtUtc`.
- `Location`: `Id`, `WorkspaceId`, `WarehouseId`, required normalized `Name`/`Code`, `CreatedAtUtc`, `ArchivedAtUtc`.
- Create warehouse plus initial location atomically; forbid empty identifiers and non-UTC timestamps; never accept `WorkspaceId` from the browser.
- SQL constraints: unique active/business names or codes per workspace for warehouses; unique location code/name per `(WorkspaceId, WarehouseId)`; FKs to `Workspaces` and `Warehouses`. Decide now whether archived identifiers remain reserved—the product precedent reserves SKU (`ProductConfiguration.cs:18-20`, tests lines 27-47).

### Option comparison

| Priority | Candidate | Value / scope assessment | Decision |
|---|---|---|---|
| 1 | Warehouse + location foundation | Directly enables physical stock ownership and is a prerequisite for safe receipt/issue/transfer. One page/API/migration using proven product conventions. | **Do next.** |
| 2 | Inventory movement | Highest eventual operational value, but unsafe now: there is no location target, stock balance/ledger, decimal precision, idempotency key, negative-stock policy, or concurrency model. | Follow P0; first movement should be one immutable receipt/adjustment, not transfers. |
| 3 | Catalog expansion (categories, prices, suppliers, product edit) | Low schema risk but does not make stock trackable; current products already establish identity/list/create/archive. | Defer unless commercial discovery says categorization/search is immediately blocking adoption. |
| 4 | Authorization/roles | Architecturally necessary before invitations/shared operations, but no non-owner role can be persisted and no member-management workflow exists. Implementing it alone creates no inventory operation. | Defer; fold permissions into the first member invitation/management slice, before shared warehouse operation. |

### Exact local integration points

**Backend additions**
- New `Domain/Entities/Warehouse.cs` and `Location.cs`, patterned on `Product.cs:7-60`; add `Archive` and normalization methods.
- New `Application/Features/Warehouses/{WarehouseModels,WarehouseHandlers,WarehouseValidators,IWarehouseCatalog}.cs`, parallel to product files. Minimum operations: `CreateWarehouseWithInitialLocation`, `ListWarehouses`, `ArchiveWarehouse`; optionally `AddLocation`/`ArchiveLocation` if still within UI scope.
- New `Infrastructure/Warehouses/EfWarehouseCatalog.cs`; creation must add both objects and call one `SaveChangesAsync`, unlike the product-only create at `EfProductCatalog.cs:13-25`.
- `Infrastructure/Persistence/ApplicationDbContext.cs:23-30`: add `DbSet<Warehouse>` and `DbSet<Location>`.
- New configurations under `Infrastructure/Persistence/Configurations/`; produce an EF migration beside `20260718112223_AddProducts.cs` and update snapshot. Use explicit indexes/FKs, not only application checks.
- `Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs:70-74`: register `IWarehouseCatalog`. No change is needed to Program for a normal controller.
- New `Api/Controllers/WarehousesController.cs`, copying the `ProductsController.cs:9-48` rule: `[Authorize]`, resolve `ICurrentWorkspace`, return `Forbid()` on null, and pass only resolved `workspace.Id` to commands.

**Frontend additions**
- Replace placeholder at `frontend/src/app/router/index.tsx:55` with lazy `features/warehouses/pages/WarehousesPage`.
- Add `features/warehouses/{types,warehouses-api,warehouses-schema}.ts` using `apiClient` and React Query patterns from products; keys must include `user.id` and `workspace.id` as at `ProductsPage.tsx:15-40`.
- Add `WarehousesPage.tsx`: list, empty/loading/error states, create form containing warehouse and initial-location fields, and archive action. The sidebar needs no change because `/warehouses` is already present (`Sidebar.tsx:29`).
- Do not wire the static dashboard warehouse/activity figures (`frontend/src/features/dashboard/pages/DashboardPage.tsx:42-96`) to this slice.

### Acceptance criteria
1. An authenticated registered owner can create a normalized warehouse with one normalized initial location; response is 201 and returns both IDs/names.
2. `GET /api/warehouses` returns only active warehouses/locations in the caller's resolved workspace; unauthenticated is 401 and missing/ambiguous workspace is 403.
3. Same warehouse/location business identifier policy is enforced by a SQL Server unique index, produces deterministic 409, and is tested with canonical-equivalent concurrent requests.
4. A user in another workspace cannot list, mutate, or archive the first user's warehouse/location; cross-workspace IDs return 404 (not existence disclosure).
5. The browser `/warehouses` page supports create, reload/list, form validation, loading/error feedback, and invalidates the workspace-keyed query after mutations.
6. Migration applies to a clean SQL Server database; unit tests cover domain normalization/archive invariants and integration tests cover transactionality, isolation, duplicate race, and archive/list behavior.
7. `dotnet build backend/InventoryFlow.sln`, `dotnet test backend/InventoryFlow.sln`, `bun run typecheck`, `bun run lint`, and `bun run build` pass for the implementation.

## Architecture

Current flow is browser `apiClient` → `[Authorize]` controller → `ICurrentWorkspace` (authenticated membership lookup) → MediatR command/query → persistence interface → EF Core SQL Server. Products demonstrate the complete pattern and the UI already keys query cache by authenticated workspace. The warehouse slice should duplicate that vertical path, with the important difference that warehouse and first location require one atomic persistence boundary.

Inventory movements should be a separate subsequent vertical slice. It can then reference the stable `Product.Id` and `Location.Id`, create immutable events, and define balance/concurrency/idempotency policies deliberately rather than embedding mutable `OnHand` on Product or Warehouse.

## Review Findings / Residual Risks

- **High — authorization expansion is blocked by the current model:** `WorkspaceMember.cs:14` rejects every role except Owner, `WorkspaceMemberRole.cs:4-8` defines only Owner, and `CurrentWorkspaceResolver.cs:18-22` selects only owner memberships. Do not claim warehouse permissions or shared-workspace roles in P0. Any invitation/member/role slice must change all three deliberately.
- **High — movement must not precede locations:** `ApplicationDbContext.cs:21-30` contains only refresh tokens, workspaces, members, and products; no warehouse, location, ledger, or balance exists. A movement first would force unvalidated physical ownership plus unresolved quantity/concurrency policy.
- **Medium — tenant filtering is convention-based:** `ProductsController.cs:21-23` and `EfProductCatalog.cs:28-34` manually propagate/filter `WorkspaceId`; there is no global tenant query filter. New warehouse/location repository methods must apply workspace predicates to every lookup/mutation and database FKs/unique indexes must scope tenant-owned values.
- **Medium — archive semantics are a product precedent, not a cross-domain policy:** products permanently reserve archived SKUs (`ProductConfiguration.cs:18-19`; `ProductEndpointsTests.cs:27-47`). Decide and test whether warehouse/location identifiers are likewise reserved before shipping; changing later may be a migration/business-rule change.
- **Low — UI coverage is manual/tooling-only:** no frontend test files/framework are present in the inspected feature tree. At minimum run the stated typecheck/lint/build and manually verify the protected route.

## Start Here

Open `backend/src/InventoryFlow.Api/Controllers/ProductsController.cs` first. It is the shortest end-to-end boundary showing the required authenticated workspace resolution and MediatR handoff; then follow its product command/catalog/configuration/test counterparts to create the warehouse/location slice consistently.

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "Concrete prioritized recommendation, exact backend/frontend integration paths and line ranges, acceptance criteria, and severity-rated review findings are provided."
    }
  ],
  "changedFiles": [],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "git status --short && git log --oneline -8; targeted find/grep/read/nl inspection",
      "result": "passed",
      "summary": "Confirmed HEAD 034b6b0 catalog delivery, no tracked source changes, and mapped product, tenancy, authorization, UI, persistence, and integration-test seams."
    }
  ],
  "validationOutput": [
    "Inspection only; no build or test run because this task prohibited edits and requested a recommendation."
  ],
  "residualRisks": [
    "High: only Owner membership is representable/resolvable; role authorization is not yet implementable without a membership-model expansion.",
    "High: movements lack warehouse/location, immutable ledger, quantity/concurrency, idempotency, and negative-stock decisions.",
    "Medium: workspace predicates are manually enforced rather than globally filtered."
  ],
  "noStagedFiles": true,
  "diffSummary": "No code edits; scouting findings written to the required agent artifact.",
  "reviewFindings": [
    "high: backend/src/InventoryFlow.Domain/Entities/WorkspaceMember.cs:14 and WorkspaceMemberRole.cs:4-8 - only Owner is supported, so roles/permissions cannot be added as a small enforcement-only slice.",
    "high: backend/src/InventoryFlow.Infrastructure/Persistence/ApplicationDbContext.cs:21-30 - no warehouse, location, movement, or stock-balance persistence exists; movement is premature.",
    "medium: backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs:18-22 and Products/EfProductCatalog.cs:28-34 - tenancy requires explicit workspace predicates in every new repository operation."
  ],
  "manualNotes": "Recommended next slice is warehouse plus required initial location; defer inventory movement until this physical foundation exists."
}
```