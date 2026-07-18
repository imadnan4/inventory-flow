# Workspace-scoped product catalog: implementation context

## Scope decision

Build the smallest complete catalog slice: authenticated **create**, active **list**, and **soft archive** of products in the server-resolved current workspace. The product has only identity/catalog fields. Explicitly exclude prices/currency, stock, categories, suppliers, descriptions/attributes, edit/restore, paging/search/filtering, role expansion, invitations, and workspace switching.

The tenancy foundation is sufficient for this slice: every newly registered user has one Owner workspace, and `ICurrentWorkspace` resolves exactly one workspace from the bearer principal plus persisted Owner membership. It deliberately fails closed if membership is absent or ambiguous.

## Recommended contract and semantics

### Product model

Add domain entity `Product : Entity<Guid>` with:

| Field | Type / rule | Reason |
| --- | --- | --- |
| `Id` | server-created `Guid`, non-empty | stable opaque product identity |
| `WorkspaceId` | server-derived non-empty `Guid`, immutable | tenant ownership boundary; never bind/accept it from HTTP or frontend state |
| `Name` | required, `Trim()`ed, 1–200 characters, case/internal spaces preserved | minimum product display identity |
| `Sku` | required normalized SKU, 1–100 characters | catalog/business identity; store the canonical value |
| `CreatedAtUtc` | required `DateTimeOffset` with zero UTC offset | audit/order metadata |
| `ArchivedAtUtc` | nullable UTC `DateTimeOffset` | archive state; `null` means active |

No user/owner ID belongs on Product: the Workspace, not one Identity user, owns it. Do not add price, cost, currency, quantity/reorder state, category/supplier foreign keys, description, `UpdatedAt`, or restore/edit fields in this slice.

Domain methods should follow the existing `Workspace`/`RefreshToken` style: validate constructor IDs and UTC timestamps, expose private setters, normalize name/SKU in static helpers, and provide an idempotent `Archive(DateTimeOffset archivedAtUtc)` that only sets `ArchivedAtUtc` once and rejects non-UTC input.

### SKU normalization and conflicts (resolved recommendation)

Canonicalize SKU as `sku?.Trim().ToUpperInvariant() ?? string.Empty`; then reject blank/whitespace-only and values over 100 characters. Preserve punctuation and **internal whitespace** (do not remove dashes/spaces or collapse whitespace): SKU formatting rules vary, and stripping characters silently aliases distinct supplier SKUs. Name is only trimmed, not case-normalized.

Enforce a unique database index on `(WorkspaceId, Sku)` (SKU is already canonical). Thus `ab-1`, ` AB-1 `, and `Ab-1` conflict **within one workspace**, while the same canonical SKU is valid in another workspace. Archive does **not** release/resurrect the SKU: an archived record remains the authoritative historical record and a create with its SKU returns a conflict. This is the safest initial semantics and avoids ambiguous identifiers before restore/edit/audit workflows exist.

Use a domain/application-specific `ProductSkuConflictException` and map it to HTTP 409 Problem Details. A pre-check can provide a clean error, but the unique index is the concurrency authority; translate SQL Server unique-index `DbUpdateException` too. Do not use a generic `DomainException` for this collision because the current global handler maps it to 400 (`GlobalExceptionHandler.cs:33-39`).

### Archive/list semantics

* `GET /api/products`: active records only (`ArchivedAtUtc == null`), restricted to the current workspace, deterministic `Name` then `Id` ascending order; return an unpaged JSON array of `ProductResponse` (`id`, `name`, `sku`, `createdAtUtc`). Do not expose tenant IDs or archived records.
* `DELETE /api/products/{id}`: soft archive only; set `ArchivedAtUtc = TimeProvider.GetUtcNow()`, never `Remove`. Return 204 if an active or already archived record belongs to the current workspace (idempotent). Return 404 if no record with both requested ID and current `WorkspaceId` exists; never reveal another workspace's product.
* `POST /api/products`: body only `{ name, sku }`; server obtains workspace and current time, returns 201 plus the response DTO. It must not receive an ID, workspace ID, owner ID, archive time, or price/stock fields.
* All three routes require `[Authorize]`. The controller resolves `ICurrentWorkspace`; a null/missing/ambiguous workspace should return `Forbid()` (403) rather than falling through to an unscoped query or making the SPA refresh its valid bearer token. No client-selected workspace header/query/body field is allowed.

## Relevant evidence and existing patterns

| Area | Files and lines | What to retain/use |
| --- | --- | --- |
| Tenant resolution | `backend/src/InventoryFlow.Application/Common/Tenancy/ICurrentWorkspace.cs:3-7`; `CurrentWorkspace.cs:3`; `backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs:10-23` | Resolve workspace from `ClaimTypes.NameIdentifier`, join persisted Owner memberships, and fail closed unless exactly one match. Inject this interface at the API tenancy boundary; do not derive a workspace from arbitrary request input. |
| Tenant schema/provisioning | `backend/src/InventoryFlow.Domain/Entities/Workspace.cs:6-33`; `WorkspaceMember.cs:6-31`; `backend/src/InventoryFlow.Infrastructure/Authentication/IdentityAuthenticationService.cs:25-42,108-131`; `WorkspaceMemberConfiguration.cs:14-22` | Registration atomically creates an Owner workspace/membership; membership is currently Owner-only. Product’s foreign key is to `Workspaces`, not `AspNetUsers`. |
| EF conventions | `ApplicationDbContext.cs:14-40`; `Persistence/Configurations/WorkspaceConfiguration.cs:8-19`; `WorkspaceMemberConfiguration.cs:8-23` | Add a `DbSet<Product>` and assembly-discovered `IEntityTypeConfiguration<Product>`. SQL Server is the production provider. Keep `ToTable`, explicit max lengths, required fields, FK delete behavior, and migration/snapshot generated by EF. |
| Application conventions | `Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs:15-28`; `Common/Behaviors/ValidationBehavior.cs:6-17`; `Features/Authentication/AuthenticationModels.cs:3-23`, `AuthenticationHandlers.cs:5-20`, `AuthenticationValidators.cs:6-27` | Feature records, MediatR handlers, FluentValidation auto-discovery. Put application port(s) in Application and implementation in Infrastructure; do not reference `ApplicationDbContext` from Application. Validation behavior turns validator failures into 400. |
| API conventions | `Api/Controllers/AuthController.cs:11-73`; `Api/Program.cs:15-21,45-59` | Attribute-routed controller, `ISender`, cancellation token, `[Authorize]`, response annotations. Program already has authentication before authorization and controllers are rate-limited. Add a `ProductsController`; no Program change is required unless adding an exception mapping requires service changes. |
| Error behavior | `Api/ExceptionHandling/GlobalExceptionHandler.cs:33-71`; `tests/.../GlobalExceptionHandlerTests.cs:14-42` | Existing validation/domain/auth mappings are 400/401/500. Add 409 conflict mapping (and a focused handler test) rather than allowing unique index races to be 500. |
| Existing tenancy proof | `tests/InventoryFlow.IntegrationTests/Api/WorkspaceMigrationAndTenancyTests.cs:43-121`; `AuthenticationEndpointsTests.cs:77-88` | SQL Server Testcontainers verifies registration provisioning, legacy backfill, resolver isolation, and ambiguous membership failure. Reuse `AuthenticatedApiFixture` for endpoint tests and unique registration credentials. |
| Domain tests | `tests/InventoryFlow.UnitTests/Domain/WorkspaceTests.cs:8-29`; `WorkspaceMemberTests.cs:8-23` | Plain xUnit invariant tests; add `ProductTests` for normalization, UTC/ID validation, and idempotent archive. |
| Router/UI | `frontend/src/app/router/index.tsx:36-58`; `components/layout/Sidebar.tsx:19-33`; `components/shared/PageHeader.tsx:3-22` | `/products` is already protected via `RequireAuth` but is a placeholder at line 48; replace only it with a lazy Products page. Sidebar already links there. Use `PageHeader`, `Card`, `Button`, current Tailwind style. |
| Browser API/session | `frontend/src/lib/api-client.ts:5-60`; `features/auth/auth-api.ts:1-26`; `app/providers/AppProviders.tsx:11-21`; `lib/query-client.ts:3-11` | All feature calls must use `apiClient`, which attaches bearer credentials and does one refresh/retry on 401. React Query and Sonner are already provided. Do not create a separate Axios instance or add workspace-selection state. |
| Form style | `frontend/src/features/auth/pages/LoginPage.tsx:9-73`; `features/auth/auth-schema.ts:2-10` | Existing pages use native form actions/FormData plus Zod, local pending/error state, accessible labels and `aria-*`; no input primitive is necessary. |

## Exact implementation file set

### Add

* `backend/src/InventoryFlow.Domain/Entities/Product.cs` — entity and normalization/archive invariants.
* `backend/src/InventoryFlow.Application/Features/Products/ProductModels.cs` — controller-safe request/response DTOs and server-only MediatR commands/queries; do not model bind commands containing `WorkspaceId`.
* `backend/src/InventoryFlow.Application/Features/Products/ProductHandlers.cs` — handlers depending on an Application product port and `TimeProvider` as appropriate.
* `backend/src/InventoryFlow.Application/Features/Products/ProductValidators.cs` — request validation for name/SKU; leave canonical domain validation authoritative.
* `backend/src/InventoryFlow.Application/Features/Products/IProductCatalog.cs` (or equivalently a clearly named application port in that feature) — list/create/archive/conflict operations without EF types.
* `backend/src/InventoryFlow.Infrastructure/Products/EfProductCatalog.cs` — workspace-filtered EF implementation, including race-safe unique violation translation.
* `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/ProductConfiguration.cs` — table, lengths, required fields, FK to `Workspace`, `(WorkspaceId, Sku)` unique index, and active-list index such as `(WorkspaceId, ArchivedAtUtc, Name, Id)`.
* `backend/src/InventoryFlow.Api/Controllers/ProductsController.cs` — protected create/list/delete endpoints; resolve `ICurrentWorkspace` and map its null result to `Forbid`.
* `backend/tests/InventoryFlow.UnitTests/Domain/ProductTests.cs`.
* `backend/tests/InventoryFlow.IntegrationTests/Api/ProductEndpointsTests.cs` — authenticated API contract/tenant isolation/SQL uniqueness coverage.
* `frontend/src/features/products/types.ts` — `Product` response type and create payload only.
* `frontend/src/features/products/products-api.ts` — `listProducts`, `createProduct`, `archiveProduct` using `apiClient`.
* `frontend/src/features/products/products-schema.ts` — Zod form rules matching public name/SKU limits (canonical server behavior remains decisive).
* `frontend/src/features/products/pages/ProductsPage.tsx` — active list with empty/loading/error states, create form, archive action, mutation pending/error handling, and query invalidation.

### Modify

* `backend/src/InventoryFlow.Infrastructure/Persistence/ApplicationDbContext.cs` — add `DbSet<Product>`.
* `backend/src/InventoryFlow.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` — register the product port implementation as scoped.
* `backend/src/InventoryFlow.Api/ExceptionHandling/GlobalExceptionHandler.cs` — map `ProductSkuConflictException` to 409 and its RFC 9110 conflict Problem Details metadata/title.
* `backend/tests/InventoryFlow.IntegrationTests/Api/GlobalExceptionHandlerTests.cs` — assert 409/Problem Details for conflict.
* `backend/src/InventoryFlow.Infrastructure/Persistence/Migrations/<timestamp>_AddProducts.cs` and `.Designer.cs`, plus `ApplicationDbContextModelSnapshot.cs` — generate using `dotnet ef migrations add AddProducts --project src/InventoryFlow.Infrastructure --startup-project src/InventoryFlow.Api` from `backend`; do not hand-author snapshot metadata.
* `frontend/src/app/router/index.tsx` — replace just `{ path: "products", element: placeholderPage("Products") }` with a lazy feature route.

No change is needed to `Program.cs`, authentication models/store, sidebar, Topbar, dashboard, or package manifests for this narrow slice.

## Test plan

1. **Domain unit:** constructor trims name and canonicalizes `sku-1` to `SKU-1`; rejects empty GUIDs, blank/oversized normalized values, non-UTC timestamps; first archive sets UTC timestamp, repeat archive does not change it.
2. **Endpoint SQL Server integration:** registration then create returns 201 with canonical SKU; active list returns it; delete returns 204, row still exists with `ArchivedAtUtc`, list excludes it, second delete is 204.
3. **Tenant boundary:** register two users/workspaces, each may create the same canonical SKU, each list sees only its own product, and user A deleting user B’s ID gets 404 while B remains active.
4. **Conflict/race:** duplicate canonical SKU in one workspace returns 409 Problem Details; archived SKU still returns 409 on new create. If practical, launch concurrent creates and assert exactly one 201 and one 409, proving database-race handling.
5. **Unauthorized/tenant unavailable:** no bearer gets 401 from `[Authorize]`; a test service setup/principal with unresolved workspace gets 403 and does not invoke an unscoped catalog operation.
6. **Frontend manual/automated:** product page calls `apiClient`; after successful create/archive invalidate `['products']`, display API validation/conflict text without a page reload, and do not send workspace IDs. Run typecheck/lint/build.

## Implementation-ready meta-prompt

> **Outcome:** Implement one workspace-scoped product catalog vertical slice: authenticated create, active list, and idempotent soft archive. Replace the protected Products placeholder with a usable product page. Do not implement prices/currency, stock, categories, suppliers, descriptions, editing/restore, paging/search/filtering, invitations/roles, or workspace switching.
>
> **Evidence/decisions:** `ICurrentWorkspace` (`backend/src/InventoryFlow.Application/Common/Tenancy/ICurrentWorkspace.cs`) returns the single server-resolved Owner workspace and the resolver fails closed for missing/ambiguous membership. Product ownership is therefore `WorkspaceId`, never `ApplicationUser.Id` and never client input. Add `Product` with `Id`, `WorkspaceId`, trimmed 1–200 `Name`, canonical 1–100 `Sku`, `CreatedAtUtc`, nullable `ArchivedAtUtc`. Canonical SKU is `Trim().ToUpperInvariant()`; retain punctuation/internal spaces. Store canonical SKU and make `(WorkspaceId,Sku)` unique. Same canonical SKU may occur across workspaces but cannot be recreated in the same workspace after archive. List only active items ordered by Name then Id. Archive only stamps `ArchivedAtUtc`; it never hard-deletes and repeated archive by an owner returns 204. Cross-workspace archive is 404.
>
> **Hard constraints:** Preserve `API -> Application -> Domain <- Infrastructure`: Application must not reference EF/Infrastructure; expose an Application product port implemented by Infrastructure. Controllers accept only `{name, sku}` and resolve `ICurrentWorkspace`; return `Forbid()` if it is null. `[Authorize]` on every Products endpoint. No workspace/user ID or archive timestamp is accepted from browser input. Use `apiClient`, never a new Axios client or workspace client state. Keep all current auth/refresh behavior unchanged. Generate the EF migration/snapshot; do not hand-edit generated metadata.
>
> **Suggested approach:** Add Product/domain tests, Application feature commands/validators/handlers/port, EF DbSet/configuration/repository and DI registration, then a `ProductsController` with `POST /api/products` (201), `GET /api/products` (200 array), and `DELETE /api/products/{id}` (204/404). Add a dedicated SKU-conflict exception and map it to 409 in the global handler; catch SQL Server unique-index races as well as pre-checking. Use `TimeProvider` for timestamps. Add a Products frontend feature (types/API/Zod/page), replace only the `/products` router placeholder, use React Query states/mutations and invalidate `['products']` after mutations.
>
> **Success criteria:** No request can create/list/archive outside the resolved workspace; no request body contains a tenant identity. DB uniqueness enforces canonical SKU per workspace under concurrency. Archived rows remain persisted yet never appear in normal list. The frontend can create/list/archive and correctly surfaces loading, empty, validation/conflict, and mutation errors. Unit and SQL Server integration coverage prove normalization, archive behavior, isolation, conflict, and unauthorized behavior.
>
> **Validation:** From repo root run `dotnet build backend/InventoryFlow.sln --no-restore`, `dotnet test backend/InventoryFlow.sln` (Docker/Testcontainers required for integration tests), then `cd frontend && bun run typecheck && bun run lint && bun run build`; run `dotnet format backend/InventoryFlow.sln --verify-no-changes --no-restore` if available. Confirm migration generation is clean and no source has an unscoped Product query.
>
> **Stop/escalate:** Escalate only if requirements change SKU reuse-after-archive semantics, ask for restore/edit/paging/pricing/stock/categories/suppliers, or require multiple active workspace selection/membership roles. Otherwise the above fully resolves the current slice; do not expand it.

## Review findings

* **High — `backend/src/InventoryFlow.Api/ExceptionHandling/GlobalExceptionHandler.cs:33-39`:** it has no 409 mapping. A product duplicate handled as `DomainException` becomes 400; an EF unique-index race becomes 500. Add an explicit conflict exception and map/catch it as described.
* **High — `backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs:16-22`:** this resolver is the only tenancy boundary and deliberately returns null on ambiguity. Any Product handler/repository that receives a client `WorkspaceId`, skips the resolver, or queries solely by product ID would bypass the foundation. Resolve once at the authorized controller boundary and include workspace predicate in every query/mutation.
* **Medium — `frontend/src/app/router/index.tsx:48`:** Products is still a protected but nonfunctional placeholder; implementing only backend endpoints would leave the requested catalog inaccessible in the existing SPA.
* **Medium — `backend/src/InventoryFlow.Infrastructure/Persistence/Migrations/20260718105902_AddWorkspaces.cs:54-70`:** migrations target SQL Server (including SQL Server-specific backfill SQL), so validate the Product unique-index/archive migration through the existing SQL Server Testcontainers fixture rather than an in-memory provider.

## Residual risks

* The current Owner-only model supports exactly one workspace and intentionally has no switching/invitations. A later multi-workspace feature must revise current-workspace selection without allowing product request inputs to select tenants.
* The recommended permanent SKU reservation after archive is a product decision. If business later requires SKU reuse, use a SQL Server filtered unique index for active products and explicitly design restore/history behavior; do not change the index casually.
* No frontend test runner is configured. Typecheck/lint/build plus manual product flow are the available frontend validation unless a test framework is intentionally added in later scope.