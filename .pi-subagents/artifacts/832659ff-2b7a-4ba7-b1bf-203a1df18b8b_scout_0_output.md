# Code Context

## Files Retrieved
1. `backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs` (lines 10-23) - the only current-workspace resolver; derives workspace solely from bearer identity + owner membership and fails closed for zero or multiple matches.
2. `backend/src/InventoryFlow.Application/Common/Tenancy/ICurrentWorkspace.cs` (lines 1-8) and `CurrentWorkspace.cs` (lines 1-4) - application-facing tenancy seam: `Task<CurrentWorkspace?> GetAsync(...)`, exposing only `Id` and `Name`.
3. `backend/src/InventoryFlow.Api/Controllers/AuthController.cs` (lines 11-73) - controller/routing/MediatR conventions and the existing authorized endpoint style.
4. `backend/src/InventoryFlow.Application/Features/Authentication/AuthenticationModels.cs` (lines 1-22), `AuthenticationHandlers.cs` (lines 1-25), and `AuthenticationValidators.cs` (lines 1-25) - feature convention: colocated request/response records, `IRequest`, handlers, and FluentValidation.
5. `backend/src/InventoryFlow.Application/Common/Behaviors/ValidationBehavior.cs` (lines 1-18) and `Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs` (lines 10-31) - validators and MediatR handlers are assembly-scanned and validation is automatic.
6. `backend/src/InventoryFlow.Infrastructure/Persistence/ApplicationDbContext.cs` (lines 11-40) and `Persistence/Configurations/WorkspaceConfiguration.cs` (lines 8-18) - EF Core DbSet/configuration convention; Product requires a DbSet and an assembly-discovered `IEntityTypeConfiguration`.
7. `backend/src/InventoryFlow.Domain/Entities/Workspace.cs` (lines 7-36) and `Common/Entity.cs` (lines 7-29) - entity convention: explicit Guid constructor, UTC timestamp guard, private setters, domain normalization/validation.
8. `backend/src/InventoryFlow.Api/ExceptionHandling/GlobalExceptionHandler.cs` (lines 13-76) - validation/domain errors map to HTTP 400 ProblemDetails; authentication exception maps to 401.
9. `backend/src/InventoryFlow.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` (lines 38-105) and `backend/src/InventoryFlow.Api/Program.cs` (lines 10-61) - `ICurrentWorkspace` is scoped; JWT auth, authorization, controllers, CORS, and rate limiting are already wired.
10. `backend/tests/InventoryFlow.IntegrationTests/Api/WorkspaceMigrationAndTenancyTests.cs` (lines 20-123 and 174-254) - SQL Server/Testcontainers migration and tenancy-isolation test precedent.
11. `frontend/src/app/router/index.tsx` (lines 25-63) - `/products` is already authenticated but is currently a placeholder at line 48.
12. `frontend/src/lib/api-client.ts` (lines 5-78), `frontend/src/features/auth/auth-api.ts` (lines 1-29), and `frontend/src/features/auth/auth-store.ts` (lines 8-23) - feature API modules use `apiClient`; bearer attachment and a single 401 refresh/retry are centralized. Auth state already holds `user.workspace`.
13. `frontend/src/app/providers/AppProviders.tsx` (lines 1-21), `frontend/src/components/shared/PageHeader.tsx` (lines 1-32), and `frontend/src/components/layout/Sidebar.tsx` (lines 18-84) - React Query, toast, shared page header, components, and Products navigation route are available.
14. `frontend/package.json` (lines 6-42) - React Query, React Hook Form, Zod, TanStack Table, and Sonner are installed; no frontend test command/framework is configured.

## Key Code

### Current-workspace boundary
`CurrentWorkspaceResolver` does **not** accept a workspace id/header/query value. It reads `ClaimTypes.NameIdentifier`, joins memberships/workspaces, filters `Owner`, takes two, and returns a workspace only when the count is exactly one:

```csharp
where member.UserId == userId && member.Role == WorkspaceMemberRole.Owner
select new CurrentWorkspace(workspace.Id, workspace.Name)
// ... return matches.Count == 1 ? matches[0] : null;
```

Every product command/query must inject `ICurrentWorkspace` and use its returned `Id` for persistence/filtering. Do not put `WorkspaceId` in the public create DTO or use a client-provided workspace identity.

### Backend slice shape
Follow the existing vertical MediatR model:
- Domain: `Domain/Entities/Product.cs`, deriving `Entity<Guid>`, owns invariants and archive transition.
- Application: `Features/Products/ProductModels.cs` (commands/queries/response records), `ProductHandlers.cs`, `ProductValidators.cs`; handlers can use `ApplicationDbContext` only if the Application project can already reference Infrastructure (it cannot by clean architecture), so add an application persistence port/repository or inspect project references before choosing. **Current auth handlers use an application interface implemented in Infrastructure**, which is the existing clean direction.
- Infrastructure: Product EF configuration plus DbContext `DbSet<Product>`; implementation of the product persistence/query port if introduced.
- API: `ProductsController` with `[ApiController]`, `[Route("api/products")]`, `[Authorize]`, injected `ISender`, cancellation tokens, `ProducesResponseType` annotations.

The first infrastructure concern is that `ApplicationDbContext` lives in Infrastructure while Application has no generic data access abstraction today; product handlers should not introduce an Application -> Infrastructure reference just to use EF.

### UI integration
Replace only router line 48 with a lazy Product page under `frontend/src/features/products/pages/ProductsPage.tsx`. The route is already nested below `RequireAuth` and `DashboardLayout`; `Sidebar.tsx` already links Products. Add a feature-local API module/types and call `apiClient`, not `authClient`, so the existing access token and retry behavior applies. Use React Query (provider exists) to fetch active products and invalidate/refetch after create/archive; use the installed form/toast/UI primitives.

## Architecture

Registration creates a personal workspace plus owner membership; login/refresh exposes it in `AuthenticatedUser.Workspace`. A subsequent browser API call sends the JWT via `apiClient`. `CurrentWorkspaceResolver` derives the only valid workspace from server-side membership. Product handlers must use that server-derived workspace id to add products, constrain the active list, and constrain archive lookup. EF maps the entity to SQL Server and a migration updates both migration source and model snapshot. The protected `/products` screen calls the products API through the shared Axios client; it must never choose tenancy from a route, store, or request payload.

## Gaps and Findings

- **High — tenancy failure response is undefined:** `CurrentWorkspaceResolver.GetAsync` returns null for an authenticated user with no owner membership *and* for ambiguous membership (`CurrentWorkspaceResolver.cs:14-22`). No reusable guard/error type or HTTP mapping exists. Each product handler/controller could accidentally dereference null or implement inconsistent behavior. Add one explicit fail-closed policy (recommended: 403 ProblemDetails for authenticated/no-unambiguous workspace) and test it.
- **High — no product data/API exists:** `ApplicationDbContext.cs:24-27` has only workspace DbSets; there is no Product entity/configuration/migration, persistence port, handler, controller, or endpoint. `/products` is only placeholder UI (`router/index.tsx:48`).
- **Medium — workspace uniqueness is a product-model decision:** current product requirements do not state whether SKU is required/unique. Decide before migration. Recommended smallest catalog contract: required normalized Name; optional normalized SKU; a non-filtered unique constraint is not appropriate for archival if recreation is expected. If SKU uniqueness is desired, use workspace-scoped uniqueness among active products (SQL Server filtered index) and define case/collation behavior.
- **Medium — archive semantics need an explicit model:** there is no soft-delete convention. Prefer `ArchivedAtUtc` nullable over hard delete so archived rows are excluded from the default list but retained for audit/future references. Archive must be idempotence-defined (recommended: first archive 204; repeated/missing/cross-workspace id returns 404, avoiding existence leakage).
- **Medium — frontend has no test runner:** `frontend/package.json:6-13` has build/lint/typecheck only. The primary executable coverage path is backend unit/integration tests; UI behavior needs either a test framework added deliberately or a documented manual/browser acceptance pass.

## Smallest Shippable Acceptance Contract

### Public API
All endpoints require a valid bearer token and resolve workspace only through `ICurrentWorkspace`.

1. `POST /api/products` body `{ "name": string, "sku"?: string }` creates an active product in the resolved workspace and returns **201** with `{ id, name, sku, createdAtUtc }`. `workspaceId`, timestamps, and archive fields are never client writable.
2. `GET /api/products` returns **200** with active products for only the resolved workspace, stable ordered (recommend `Name`, then `Id`), each `{ id, name, sku, createdAtUtc }`. It excludes archived products.
3. `DELETE /api/products/{id}` archives only a product in the resolved workspace and returns **204**. It must not delete physical data.
4. Unauthenticated requests return 401. Invalid create payloads return the existing 400 validation ProblemDetails. An authenticated request without exactly one current workspace fails closed using the chosen consistent status. Cross-workspace product ids must return indistinguishably from missing ids (recommended 404).

### Product invariants recommended for this slice
- Guid product/workspace ids cannot be empty; `WorkspaceId` is immutable/private.
- Name: trim, 1..200 (or a selected documented maximum), reject leading/trailing input in FluentValidation consistently with registration; domain normalizes defensively.
- SKU: optional; when supplied trim and validate an agreed maximum. Do not add price, stock, category, supplier, images, pagination, editing, restore, or workspace switching to this slice.
- `CreatedAtUtc` and `ArchivedAtUtc` must be UTC. `Archive(now)` refuses invalid/non-UTC time and makes archive state explicit.

### UI
At `/products`, render a Products header, active-products loading/empty/error/list states, and an Add product form/dialog for Name (+ optional SKU). On successful create/archive, refresh the list and show feedback. The existing session's `user.workspace` may be displayed as context but must not be submitted as tenancy selection. Archive confirmation and a per-row Archive action are sufficient; no archived view/restore required.

## Migration and Testing Concerns

- Add `Products` with FK `WorkspaceId -> Workspaces` (cascade delete consistent with `WorkspaceMembers`), required name/created timestamp, nullable SKU and `ArchivedAtUtc`, plus an index starting with `WorkspaceId` for list/filter. Generate EF migration from the Infrastructure startup/project and commit both `.cs`, `.Designer.cs`, and `ApplicationDbContextModelSnapshot.cs`; do not hand-edit only the snapshot.
- The existing workspace migration is SQL Server-specific and tests actual migration sequencing using Testcontainers (`WorkspaceMigrationAndTenancyTests.cs:174-254`). Product migration should run against the same provider. No product backfill is needed because the table is new.
- Unit-test Product construction/normalization, invalid IDs/UTC, and archive state. Application handler tests should prove resolver workspace is used rather than command input.
- Integration-test: authenticated owner create -> list includes it; archive -> absent from list but row remains with `ArchivedAtUtc`; two users/workspaces cannot list or archive each other's rows; invalid payload -> 400; missing JWT -> 401; null/ambiguous current workspace -> chosen fail-closed response. Use `AuthenticatedApiFixture` for a fresh migrated DB and existing registration/login helpers as precedent.
- Run `dotnet test backend/InventoryFlow.sln` (Docker required for integration tests) and `bun run lint && bun run typecheck && bun run build` in `frontend`. Add UI automation only if a test framework is introduced.

## Start Here

Open `backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs` first. It is the non-negotiable server-side tenancy boundary; settle the null-workspace HTTP policy before designing handlers/controllers. Then add the product domain/persistence contract around that seam and replace the existing frontend products placeholder.