# Catalog-management vertical-slice context

## Executive finding

The repository has a working authentication foundation and a protected `/products` SPA route, but **no catalog domain model, persistence, product API, tenancy model, role assignment, or product UI**. The lowest-risk first slice is an authenticated, owner-scoped product list/create flow—not categories, stock, prices/currency, editing, deletion, images, or warehouse inventory.

## Architecture and concrete evidence

| Layer | Existing convention / evidence | Implication for a catalog slice |
|---|---|---|
| Domain | `backend/src/InventoryFlow.Domain/Common/Entity.cs:7-22` is the base entity; `Entities/RefreshToken.cs:9-104` validates constructor invariants and exposes private setters/lifecycle methods; `Exceptions/DomainException.cs:6-25` is the only domain exception. | Add `Entities/Product.cs`, deriving from `Entity<Guid>`, with constructor validation. Preserve private setters and domain-owned normalization/invariants. |
| Application | `Features/Authentication/AuthenticationModels.cs:12-21` keeps command/query records and response DTOs together; `AuthenticationHandlers.cs:5-19` has thin MediatR handlers delegating to an application port; `IAuthenticationService.cs:4-15` is implemented in Infrastructure. Validators are separate in `AuthenticationValidators.cs:6-24`. Assembly scanning registers handlers/validators (`ApplicationServiceCollectionExtensions.cs:22-28`) and `ValidationBehavior.cs:7-18` invokes them before handlers. | Mirror this feature folder/port pattern: product request/response records, `IProductCatalogService` (or repository) in Application, MediatR handlers, and FluentValidation validators. No manual DI changes should be needed for handlers/validators; register the Infrastructure port implementation in `AddInfrastructure`. |
| Infrastructure | One SQL Server EF context (`Persistence/ApplicationDbContext.cs:14-34`) applies all `IEntityTypeConfiguration`s by assembly scan. Existing `RefreshTokenConfiguration.cs:14-40` explicitly names table, sets lengths/indexes/FK/deletion. `IdentityAuthenticationService.cs:13-146` is the precedent for an Infrastructure implementation consuming `ApplicationDbContext`. | Add `DbSet<Product>`, `ProductConfiguration`, and an Infrastructure catalog service that always receives the authenticated owner ID as an argument. Generate a real EF migration plus designer/snapshot; do not hand-edit only the snapshot. |
| API | `AuthController.cs:12-73` is attribute-routed and sends MediatR requests. Bearer authentication is wired in `InfrastructureServiceCollectionExtensions.cs:48-63`; middleware order is correct in `Program.cs:45-59`. `AuthController.cs:64-72` reads `ClaimTypes.NameIdentifier` and rejects invalid/missing GUIDs. | Create `ProductsController` with `[ApiController]`, `[Route("api/products")]`, `[Authorize]`, `ISender`, and a helper/parameter-derived authenticated user ID. Never accept owner/tenant/user ID from the request body or route. Controller should return 401 if the name-id claim is invalid. |
| Error behavior | `GlobalExceptionHandler.cs:33-71` maps FluentValidation and `DomainException` to 400, authentication exception to 401; unexpected persistence failures become 500. | Validators cover request shape. A pre-insert SKU check alone is race-prone: retain a DB unique index and translate a duplicate-key save failure to a handled client error (currently likely a `DomainException`/400 unless error taxonomy is expanded). |
| Tests | Domain tests exercise constructors/lifecycle (`tests/InventoryFlow.UnitTests/Domain/RefreshTokenTests.cs:9-95`). SQL-backed integration fixture runs all migrations (`AuthenticatedApiFixture.cs:14-69`); auth endpoint tests construct bearer requests (`AuthenticationEndpointsTests.cs:53-69`). Tests are globally nonparallel (`TestCollectionBehavior.cs:1-3`). | Add Product domain tests and Product endpoint integration tests using this fixture. No existing frontend test runner is configured (`frontend/package.json` only has dev/build/lint/typecheck scripts). |
| React | The route is already protected: `frontend/src/app/router/index.tsx:36-60`; specifically `products` is presently a placeholder at line 48. The sidebar already links it (`components/layout/Sidebar.tsx:23-35`). `RequireAuth.tsx:3-21` gates private routes. Axios attaches bearer tokens and refreshes/retries one 401 (`lib/api-client.ts:46-61`). React Query exists with 30s stale data (`lib/query-client.ts:3-11`). `PageHeader.tsx:3-20`, cards/buttons, Tailwind aliases, and lazy route modules are established UI conventions. | Replace only the products placeholder with a lazy `features/products/pages/ProductsPage` module. Put product API/types/query/mutation code under `features/products`, use `apiClient` (not `authClient`), React Query for list/invalidation, `PageHeader` + existing UI primitives. The sidebar needs no change. |

## Current routes and API surface

**SPA:** public `/login`, `/register`; authenticated dashboard layout at `/` redirects to `/dashboard`; placeholder private routes include `/products`, `/categories`, `/inventory`, suppliers, warehouses, orders, reports, users, and settings (`frontend/src/app/router/index.tsx:25-62`).

**API:** only `/health` and `/api/auth/{register,login,refresh,logout,me}` currently exist (`Program.cs:58-59`, `AuthController.cs:13-73`). No products endpoint exists. API requests have a fixed-window `api` limiter of 100/min (`Program.cs:23-33`) and browser CORS permits credentials and configured origins (`Program.cs:34-41`).

## Smallest useful product scope (recommended)

### Product model and rules

Implement a `Product` aggregate/entity with:

* `Id: Guid`, generated server-side;
* `OwnerUserId: Guid`, derived server-side from authenticated JWT subject/name identifier;
* `Name: string`, required, trimmed, max 200;
* `Sku: string`, required, trimmed, max 100, normalized consistently (recommended `ToUpperInvariant()`), unique **per owner**.

This supports the first real catalog and the future inventory identifier without prematurely deciding currency, units of measure, categories, taxonomy, pricing, stock balances, images, soft-delete, or audit requirements. The entity should reject empty IDs/blank inputs/overlength fields; application FluentValidation should mirror the input constraints to produce validation-problem responses before the handler.

### Endpoints and client behavior

* `GET /api/products` → 200 and an owner-filtered list, sorted predictably (recommended name then SKU); return DTOs only.
* `POST /api/products` with `{ name, sku }` → 201 Created and DTO. Its location can be `/api/products/{id}` even if a get-by-id endpoint is deferred.
* Keep `GET /api/products/{id}`, `PUT/PATCH`, delete, search/paging, categories, and inventory out of this vertical slice.
* UI: `/products` displays a page header, small create-product form (name/SKU), loading/empty/error states, and product table/list. On successful mutation invalidate the products list and use the existing `Toaster` facility if desired. Browser authentication is already handled by `apiClient`; no access-token persistence or additional route guard is required.

## Persistence and migration requirements

1. Add `Products` to `ApplicationDbContext` (`ApplicationDbContext.cs:18-26` is the analogous `DbSet` location).
2. Add `ProductConfiguration` discovered via existing `ApplyConfigurationsFromAssembly`:
   * table `Products`, PK `Id`;
   * required `OwnerUserId`, `Name nvarchar(200)`, `Sku nvarchar(100)` (exact column types/lengths should follow EF SQL Server conventions as existing config does);
   * unique composite index `(OwnerUserId, Sku)`;
   * index `(OwnerUserId, Name)` for list ordering/filtering;
   * FK from `OwnerUserId` to `ApplicationUser.Id`. Choose deletion behavior deliberately; `Restrict`/`NoAction` avoids accidental catalog deletion with a user, whereas `Cascade` matches the existing refresh-token-only precedent (`RefreshTokenConfiguration.cs:36-39`). For this SaaS data, restrict is safer unless an explicit account-deletion policy says otherwise.
3. Restore local EF tool if necessary and run `dotnet ef migrations add AddProducts --project src/InventoryFlow.Infrastructure --startup-project src/InventoryFlow.Api` from `backend`, with `ConnectionStrings__InventoryFlowDatabase` configured. This produces migration `.cs`, designer, and changes snapshot. Apply/test via the documented `dotnet ef database update ...` command (`README.md:93-95`). `ApplicationDbContextFactory.cs:17-31` requires that environment variable for design time.
4. Do not add new packages: EF Core SQL Server, MediatR, FluentValidation, React Query, React Hook Form, Zod, and React Table are already available (`backend/Directory.Packages.props`, `frontend/package.json`).

## Multi-tenancy and authorization implications

### Blocker-level product decision

**[BLOCKER] There is no tenant/workspace/organization or membership model.** The product describes a SaaS workspace (`README.md:3`), but persistence has only `ApplicationUser`, Identity roles, refresh tokens, and data-protection keys (`ApplicationDbContext.cs:14-26`; model snapshot starts domain data at `ApplicationDbContextModelSnapshot.cs:25-58`). JWT claims only include user identity/email/display name (`JwtAccessTokenIssuer.cs:15-21`). Although Identity roles are registered (`InfrastructureServiceCollectionExtensions.cs:76-91`), registration assigns none (`IdentityAuthenticationService.cs:25-31`) and there are no policies/claims/role checks beyond authenticated access.

Therefore, do **not** call a simple `OwnerUserId` catalog solution multi-tenant. It is a safe **per-user isolation** slice: every read filters by owner; creation supplies current user; all future by-ID mutations/deletes must include owner in the query/predicate so IDs cannot cross users. It will need a data migration to introduce workspace ownership later.

If the intended acceptance means true shared-workspace catalog management, tenancy is a prerequisite scope expansion: add `Workspace`/`Organization`, `WorkspaceMember(UserId, WorkspaceId, role)`, an active-workspace resolution contract/claim/header, `WorkspaceId` on Product and unique `(WorkspaceId, Sku)`, registration bootstrap membership, and authorization policy/role decisions. No current code provides a safe active-workspace selection mechanism. Escalate for this choice before implementing true tenancy.

### Authorization minimum

For the owner-scoped slice, `[Authorize]` is sufficient for access authentication; ownership filtering is the actual authorization. Do not expose a client-provided owner ID. Roles/permissions should not be invented for this slice because current accounts never receive roles and the README explicitly lists role/permission enforcement as future work (`README.md:139`). If a product-management permission is required, it necessarily expands scope to role seeding/assignment and authorization policy tests.

## Tests and validation plan

* **Domain unit tests:** constructor rejects empty owner ID, blank/whitespace name/SKU, excess lengths; normalizes accepted values; no externally mutable fields. Follow `RefreshTokenTests.cs` style.
* **Application/unit tests (if handlers contain logic):** handlers pass owner ID and requests to the port; validators reject invalid create input. Thin-delegation handlers can rely mainly on integration coverage, matching authentication’s existing approach.
* **Integration tests:** use `AuthenticatedApiFixture` so migrations run against isolated SQL Server. Register two users; create/list for user A; verify unauthenticated GET/POST returns 401; verify A sees only A; verify duplicate SKU for same owner is a client error; verify same SKU for B succeeds; verify create response/status/body. Bearer token construction follows `AuthenticationEndpointsTests.cs:53-69`.
* **Frontend:** no test framework is installed. Ensure `bun run typecheck`, `bun run lint`, and `bun run build` pass; manually register/login, navigate to `/products`, create a product, reload/list it, and verify no manual Authorization code bypasses `apiClient`.
* **Backend checks:** `dotnet build backend/InventoryFlow.sln --no-restore`, `dotnet test backend/InventoryFlow.sln` (requires Docker for Testcontainers integration tests), and `dotnet format backend/InventoryFlow.sln --verify-no-changes --no-restore`.

## Review findings

* **blocker — absent workspace tenancy:** `ApplicationDbContext.cs:14-26` and `JwtAccessTokenIssuer.cs:15-21` prove neither a tenant key nor a workspace context exists. Decide owner-scoped first slice versus prerequisite workspace/membership scope before claiming multi-tenancy.
* **high — duplicate SKU race/error mapping:** DB-level composite uniqueness is necessary, but `GlobalExceptionHandler.cs:33-71` turns unhandled `DbUpdateException`s into 500. The implementation must translate duplicate-key persistence failures, not only precheck.
* **medium — authorization is authentication-only today:** `[Authorize]` works, but registered users have no roles and no policies (`IdentityAuthenticationService.cs:25-31`, `InfrastructureServiceCollectionExtensions.cs:76-91`). Owner filtering must be in every catalog data operation.
* **medium — tests require SQL Server container runtime:** integration coverage depends on Testcontainers (`AuthenticatedApiFixture.cs:22-69`); test execution may fail where Docker is unavailable.

## Residual risks / unresolved product decisions

* Whether catalog records are user-private (recommended first slice) or shared per organization is the sole scope decision that cannot be inferred from code.
* Currency, price/cost, units, category relation, status/archive/deletion, SKU case sensitivity, and audit timestamps are unspecified. This handoff intentionally excludes them; adding any requires explicit rules and likely schema/API/UI expansion.
* The API currently has no conflict exception/status convention. Keeping duplicate SKU as a 400 via current `DomainException` behavior is compatible with existing handling but semantically less precise than 409; do not change global error taxonomy without an approved broader API decision.

## Implementation-ready meta-prompt

> **Goal:** Implement the smallest authenticated catalog vertical slice: owner-scoped products can be created and listed through a protected ASP.NET Core API and the existing `/products` React route, backed by a SQL Server EF migration.
>
> **Evidence/context:** Follow `RefreshToken` for a validating Domain entity (`backend/src/InventoryFlow.Domain/Entities/RefreshToken.cs`) and the Authentication feature’s request/handler/validator/application-port/Infrastructure-service pattern (`backend/src/InventoryFlow.Application/Features/Authentication/*`, `backend/src/InventoryFlow.Infrastructure/Authentication/IdentityAuthenticationService.cs`). Add `DbSet`/configuration via `ApplicationDbContext.cs` and `Persistence/Configurations`; configuration scanning is automatic. API uses controllers + `ISender`; derive `Guid` from `ClaimTypes.NameIdentifier` as `AuthController.Me` does. SPA `/products` is an authenticated placeholder at `frontend/src/app/router/index.tsx:48`; sidebar link already exists. Use `apiClient`, React Query, `PageHeader`, and existing UI primitives.
>
> **Outcome/scope:** Add a Product with server-generated ID and server-derived `OwnerUserId`, required trimmed name (max 200) and normalized required SKU (max 100). Add only `POST /api/products` and `GET /api/products`; return owner-filtered DTOs, deterministic list order, and 201 for create. Replace the placeholder with a lazy Products page containing create form plus loading/empty/error/list states. Add EF configuration with composite unique `(OwnerUserId, Sku)`, owner/list indexes and a deliberate FK delete behavior, then generate the EF migration/designer/snapshot. Add domain and SQL-backed integration tests. Do not implement categories, pricing/currency, stock, edits/deletes, pagination, role management, or true workspace tenancy.
>
> **Hard constraints:** Every data operation scopes by the authenticated owner; never receive owner/user/tenant ID from the client. `[Authorize]` is required on both endpoints. Preserve layer direction: Domain must not depend on EF/API; Application must not depend on Infrastructure; Infrastructure implements the Application port. Preserve existing JWT refresh and auth client behavior. Do not claim multi-tenancy; this is user isolation.
>
> **Success criteria:** An authenticated user can create and subsequently list only their products; an unauthenticated request is 401; a duplicate SKU for the same owner is safely a non-500 client error while identical SKU for another owner succeeds; migration applies to clean database; `/products` works using bearer/refresh behavior already in `apiClient`.
>
> **Validation:** Run EF migration generation with `ConnectionStrings__InventoryFlowDatabase`; `dotnet build backend/InventoryFlow.sln --no-restore`; `dotnet test backend/InventoryFlow.sln` (Docker required); `dotnet format ... --verify-no-changes --no-restore`; then `cd frontend && bun run typecheck && bun run lint && bun run build`. Manually exercise login → products → create → reload.
>
> **Stop/escalate:** Stop and ask the supervisor before building a workspace/tenant/membership model, role/permission enforcement, price/currency, categories, or a global 409 error taxonomy. Otherwise owner-scoped create/list has sufficient evidence; do not expand scope.
>
> **Resolved assumptions:** First slice is per-user owner-scoped; SKU is unique case-insensitively by normalized storage within that owner; name/SKU only are catalog attributes; server returns DTOs rather than EF/domain objects.

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "Concrete architecture, route, persistence, authorization, risk, and test findings cite exact repository paths and line ranges; review findings carry blocker/high/medium severities."
    }
  ],
  "changedFiles": [],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "dotnet build backend/InventoryFlow.sln --no-restore",
      "result": "passed",
      "summary": "All backend projects built with 0 warnings and 0 errors."
    },
    {
      "command": "cd frontend && bun run typecheck && bun run lint",
      "result": "passed",
      "summary": "TypeScript no-emit check and ESLint completed successfully."
    },
    {
      "command": "git diff --check",
      "result": "passed",
      "summary": "No tracked working-tree diff or whitespace errors; only runtime .pi-subagents artifacts are untracked."
    }
  ],
  "validationOutput": [
    "Backend build: succeeded, 0 warnings, 0 errors.",
    "Frontend typecheck and lint: passed.",
    "No source files were edited for this context-only task."
  ],
  "residualRisks": [
    "No workspace/tenant/membership model exists; owner-scoping is not true multi-tenancy.",
    "Duplicate-key persistence errors require explicit translation or a concurrent create can return 500.",
    "Integration test execution requires Docker/Testcontainers and was not run."
  ],
  "noStagedFiles": true,
  "diffSummary": "No repository source changes; context handoff written only to the required runtime artifact path.",
  "reviewFindings": [
    "blocker: backend/src/InventoryFlow.Infrastructure/Persistence/ApplicationDbContext.cs:14-26 - no tenant/workspace or membership persistence exists.",
    "high: backend/src/InventoryFlow.Api/ExceptionHandling/GlobalExceptionHandler.cs:33-71 - unhandled duplicate database writes map to 500.",
    "medium: backend/src/InventoryFlow.Infrastructure/Authentication/IdentityAuthenticationService.cs:25-31 - registration assigns no roles, so permission enforcement is not ready."
  ],
  "manualNotes": "Implementation-ready meta-prompt and all requested context are included above; no code edits were performed."
}
```