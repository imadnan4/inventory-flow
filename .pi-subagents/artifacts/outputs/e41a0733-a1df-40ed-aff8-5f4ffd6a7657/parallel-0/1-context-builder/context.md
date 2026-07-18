# Post-ledger implementation context

## Recommendation: Supplier catalog vertical slice

Implement **workspace-scoped supplier catalog CRUD limited to create, active-list, and idempotent archive**, with the existing `/suppliers` UI placeholder replaced by a supplier page. This is the smallest useful next slice: suppliers are already a visible navigation promise (`frontend/src/app/router/index.tsx:57`), are a prerequisite for purchase orders/receipts, and can use the proven catalog pattern without changing the newly completed ledger.

Do **not** add purchase orders/receipts, supplier-product pricing, payment terms, product-supplier links, transfer movements, sales orders, roles/permissions, or reporting in this slice. A purchase receipt must eventually create receipt ledger entries atomically and needs its own document/lifecycle design. A transfer needs paired, all-or-nothing ledger effects across balances. Both are materially riskier than this catalog slice.

## Current map and evidence

### Catalog and warehouses
- `backend/src/InventoryFlow.Domain/Entities/Product.cs:7-59`: workspace-owned active/archive lifecycle; canonical SKU; `NameMaxLength=200`, `SkuMaxLength=100`; UTC timestamps.
- `backend/src/InventoryFlow.Application/Features/Products/ProductModels.cs:7-19`, `ProductHandlers.cs:7-31`, `ProductValidators.cs:7-16`, and `IProductCatalog.cs:7-28`: the complete application pattern: request/response, MediatR commands/query, FluentValidation, persistence port and conflict exception.
- `backend/src/InventoryFlow.Infrastructure/Products/EfProductCatalog.cs:15-70`: EF adapter filters every lookup/list by workspace, maps SQL Server unique-index errors, and archives idempotently.
- `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/ProductConfiguration.cs:11-20`: required fields, unique workspace SKU, active-list index, workspace FK. Use this as the migration/configuration model.
- `backend/src/InventoryFlow.Api/Controllers/ProductsController.cs:10-48`: authenticated controller obtains workspace only from `ICurrentWorkspace`, never from request input; routes are POST/GET/DELETE under `/api/products`.
- Warehouses mirror the above structure in `Application/Features/Warehouses`, `Infrastructure/Warehouses`, controller, configuration, page, and integration tests. Supplier should copy the *shape*, not add cross-domain coupling.

### Ledger constraints (must remain untouched)
- `backend/src/InventoryFlow.Api/Controllers/InventoryController.cs:10-55`: only authenticated receipt, issue, and balance routes exist.
- `backend/src/InventoryFlow.Infrastructure/Inventory/EfInventoryLedger.cs:13-57`: ledger uses retry execution strategy plus serializable transaction; idempotency is workspace scoped; only active product and warehouse sources can move; balance is materialized atomically.
- `backend/src/InventoryFlow.Infrastructure/Inventory/EfInventoryLedger.cs:61-67`: balances are workspace scoped.
- `backend/tests/InventoryFlow.IntegrationTests/Api/InventoryEndpointsTests.cs:31-218`: SQL-backed coverage explicitly protects idempotency, non-negative stock, decimal bounds, and archive-vs-receipt concurrency. Suppliers must not alter these invariants.

### API, persistence, errors and DI
- `backend/src/InventoryFlow.Infrastructure/Persistence/ApplicationDbContext.cs:13-47`: add only a `DbSet<Supplier>` alongside existing domain sets; configurations auto-load via `ApplyConfigurationsFromAssembly` at line 45.
- `backend/src/InventoryFlow.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs:66-68`: register the new supplier port/EF adapter here.
- `backend/src/InventoryFlow.Api/ExceptionHandling/GlobalExceptionHandler.cs:31-67`: add a supplier duplicate-conflict exception mapping to 409 (and ensure its details are not exposed inconsistently); domain/validation violations already map to 400.
- Existing migration chain ends with `backend/src/InventoryFlow.Infrastructure/Persistence/Migrations/20260718123000_ProtectInventorySourceArchival.cs`; generate a new EF migration and update model snapshot—do not hand-edit a partial/old migration.

### UI
- `frontend/src/app/router/index.tsx:49-59`: suppliers currently render a placeholder; replace only this lazy route with `@/features/suppliers/pages/SuppliersPage`.
- `frontend/src/components/layout/Sidebar.tsx:24-36`: Suppliers already has nav entry and icon; no sidebar change required.
- `frontend/src/features/products/pages/ProductsPage.tsx:15-126`: exact page precedent: React Query key includes both `user.id` and `workspace.id`; list/create/archive mutations invalidate the scoped key; zod validates FormData; RFC7807 `detail/title` is displayed.
- `frontend/src/features/products/products-api.ts:3-13`, `products-schema.ts:3-6`, `types.ts:1-11`: pattern for a small feature-local API/schema/types layer. `frontend/src/features/inventory/pages/InventoryPage.tsx:17-104` independently confirms workspace-qualified query keys and safe mutation retry behavior.

### Tests and validation baseline
- `backend/tests/InventoryFlow.IntegrationTests/Api/ProductEndpointsTests.cs:16-125`: best direct test template: create/list/archive, canonical uniqueness including archived row, two-workspace isolation, and concurrent unique insert behavior against SQL Server.
- `backend/tests/InventoryFlow.UnitTests/Domain/ProductTests.cs` is the direct domain-test template.
- Baseline command passed: `dotnet test backend/InventoryFlow.sln --no-restore` — 22 unit + 26 SQL Server integration tests passed.

## Minimal data/API contract

Use a `Supplier` entity with only:
- `Id`, `WorkspaceId`, `Name`, `CreatedAtUtc`, `ArchivedAtUtc`.
- Normalize name by trimming, reject blank, and use the same 200-character maximum as product/warehouse unless product requirements deliberately select another bound.
- A unique `(WorkspaceId, Name)` index (the normalized persisted name is the uniqueness key); archived names stay reserved, matching product SKU lifecycle. Active list order: `Name`, then `Id`.

Endpoints:
- `POST /api/suppliers` body `{ name }` → 201 `SupplierResponse`.
- `GET /api/suppliers` → active suppliers only, current workspace only.
- `DELETE /api/suppliers/{id}` → 204 for first/repeat same-workspace archive; 404 for missing/cross-workspace.

This scope deliberately has no relation to products, warehouses, movements, or balances. That makes archive safe now; when supplier references exist in a later purchasing slice, its lifecycle rule must be specified then.

## Implementation meta-prompt

**Goal**: Deliver the supplier-catalog vertical slice end to end: authenticated workspace-scoped create/list/idempotent-archive API, SQL Server persistence/migration, and the existing Suppliers UI route, following the existing product catalog conventions.

**Evidence/constraints**:
- Follow `Product` and its application/EF/controller/test/UI files cited above; establish `Features/Suppliers`, `Infrastructure/Suppliers`, entity/configuration/controller, and frontend `features/suppliers` equivalents.
- Workspace identity is server-resolved via `ICurrentWorkspace`; never accept it from browser input. Every EF query must predicate `WorkspaceId`.
- Make an EF-generated migration from the current tip and update snapshot. Register its port/adapter in infrastructure DI. Add a 409 exception mapping for duplicate normalized supplier name.
- Query keys must include user and workspace, matching `ProductsPage`; invalidate the scoped supplier key after create/archive.
- Keep the ledger and its movement semantics unchanged. Do not implement purchase flows, ledger integration, product association, transfers, sales, authorization redesign, or supplier editing.

**Success criteria**:
1. Supplier domain normalization/lifecycle and active-list persistence are implemented with tenant-safe unique name enforcement.
2. API has POST/GET/DELETE behavior above with 401/403 consistent with existing authenticated endpoints, 404 cross-tenant/missing behavior, and 409 duplicate behavior.
3. `/suppliers` is a lazy real page, not a placeholder, and supports list/create/archive plus visible API errors.
4. Tests cover domain normalization/archive plus SQL-backed create/list/archive, duplicate (including race if matching product precedent), workspace isolation, and authorization.

**Validation**: run `dotnet test backend/InventoryFlow.sln --no-restore`; then from `frontend`, run `bun run lint`, `bun run typecheck`, and `bun run build`. If Testcontainers/SQL Server is unavailable, report the exact integration limitation rather than replacing integration coverage with an in-memory provider.

**Stop/escalation**: stop and ask before adding any supplier-to-product or document/ledger relationship, changing archive semantics beyond this standalone catalog, or changing authorization policy. Otherwise the above is fully determined by local patterns.

## Review findings
- **High (scope risk):** Introducing purchase receipts/orders as the next increment would require a new aggregate/status model and atomic receipt-to-ledger posting. `EfInventoryLedger` currently accepts only direct Receipt/Issue (`:13-57`), so linking a document afterward risks duplicate or non-atomic stock.
- **High (scope risk):** Warehouse transfer cannot safely be implemented by issuing then receiving via existing endpoints; it needs one transaction/idempotency identity over both balance rows and paired audit entries.
- **Medium (contract risk):** Do not use a global supplier-name uniqueness constraint. Product precedent proves all catalog uniqueness is workspace-scoped (`ProductConfiguration.cs:18` and `ProductEndpointsTests.cs:56-77`).
- **Medium (frontend isolation risk):** A supplier React Query key without user/workspace can leak cached data after a session/workspace change; retain the product/inventory key pattern.

## Residual risks
- Product and warehouse archive protections are necessary because they are ledger sources; supplier archive is presently unconstrained only because no relation exists. A purchasing slice must define supplier archival when documents reference it.
- There is no existing frontend component test framework/configuration in the discovered tree; backend integration tests and frontend lint/typecheck/build are the available targeted checks.

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "Concrete catalog, warehouse, ledger, API, UI, test, DI, migration, and error-handling findings include paths/line numbers and severity-ranked findings."
    }
  ],
  "changedFiles": [
    ".pi-subagents/artifacts/outputs/e41a0733-a1df-40ed-aff8-5f4ffd6a7657/parallel-0/1-context-builder/context.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "dotnet test backend/InventoryFlow.sln --no-restore",
      "result": "passed",
      "summary": "22 unit and 26 SQL Server integration tests passed."
    }
  ],
  "validationOutput": [
    "Repository baseline is green; this was a no-code context task."
  ],
  "residualRisks": [
    "Purchase and transfer flows need separate atomic ledger/document design.",
    "Supplier archive lifecycle must be revisited when purchasing references are introduced."
  ],
  "noStagedFiles": true,
  "diffSummary": "Only the required context handoff artifact was written; no product code was edited.",
  "reviewFindings": [
    "high: EfInventoryLedger.cs:13-57 - purchase receipt or transfer must not be bolted onto direct receipt/issue endpoints because atomic document/pair semantics are absent.",
    "medium: ProductConfiguration.cs:18 - supplier uniqueness must be workspace scoped, not global."
  ],
  "manualNotes": "Recommended minimal-safe next slice is standalone supplier catalog; it unlocks purchasing without changing the verified ledger."
}
```