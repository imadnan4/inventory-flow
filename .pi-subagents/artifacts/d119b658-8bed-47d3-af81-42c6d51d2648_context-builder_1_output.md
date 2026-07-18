# Inventory foundation context — recommended next slice

## Recommendation

**Deliver workspace-scoped warehouse catalog CRUD/archive only; do not introduce stock quantities, locations, transfers, adjustments, or stock movements in this slice.** This is the smallest useful inventory foundation that cannot misstate on-hand inventory: it establishes a tenant-owned physical container while making **no inventory-accounting claim**. A `Location` needs a parent warehouse and a movement needs a product, a source/destination model, an immutable ledger, and an atomic balance/concurrency strategy; adding any partial variant would create a misleading inventory feature.

Suggested user-visible scope:

* `POST /api/warehouses` — create `{ name }` in the server-resolved workspace.
* `GET /api/warehouses` — active warehouses, deterministic name/id order.
* `DELETE /api/warehouses/{id}` — idempotent archive, 404 outside the current workspace.
* A protected `/warehouses` React page replacing the existing placeholder, with list/create/archive and workspace-aware React Query key.

Model only `Warehouse(Id, WorkspaceId, Name, CreatedAtUtc, ArchivedAtUtc)`. Use product-like trim/required/max-length rules and a DB unique `(WorkspaceId, Name)` index (or a canonical normalized name column if product requirements later demand case-insensitive uniqueness independent of SQL collation). Archive rather than hard-delete, because a future location or immutable movement ledger must retain its warehouse reference. Keep archived name reserved to avoid historical ambiguity. Do **not** add a `quantity`/`onHand` column to `Product` or `Warehouse`.

## Evidence and integration map

### Existing vertical-slice pattern

| Area | Existing evidence | Warehouse adaptation |
|---|---|---|
| Domain | `backend/src/InventoryFlow.Domain/Entities/Product.cs:7-61` owns normalization, required IDs, UTC timestamp rule, and idempotent archive. `Workspace.cs:7-34` is the tenancy parent. | Add `Warehouse.cs`; same private setters and constructor invariants. Set an intentional `NameMaxLength` (200 is the nearest local catalog precedent). No location or balance state. |
| Application | `Features/Products/ProductModels.cs:6-19`, `ProductHandlers.cs:7-35`, `ProductValidators.cs:7-16`, and `IProductCatalog.cs:6-23` are command/query/handler/port separation. Validator behavior is globally registered (`ApplicationServiceCollectionExtensions.cs:16-24`). | Create parallel `Features/Warehouses` request/response, commands/queries, validator, handlers, `IWarehouseCatalog`, and a duplicate-name exception. Commands receive `WorkspaceId` only from controller, never request DTO. |
| API/tenancy | `ProductsController.cs:10-48` is `[Authorize]`; each action asks `ICurrentWorkspace`, returns `Forbid()` on null, and only then dispatches workspace-id commands. `CurrentWorkspaceResolver.cs:10-24` derives a single Owner workspace from authenticated claims/membership and returns null for no/ambiguous match. | Clone this boundary exactly at `api/warehouses`; no workspace ID route/query/body parameter. Cross-workspace ID lookups must return 404, not reveal existence. |
| Persistence | `ApplicationDbContext.cs:21-24` exposes entity DbSets; `ProductConfiguration.cs:10-22` sets table, max lengths, FK cascade, unique and list indexes. `EfProductCatalog.cs:10-38` scopes all reads by workspace and translates SQL Server unique errors 2601/2627. | Add `DbSet<Warehouse>`, `WarehouseConfiguration`, `EfWarehouseCatalog`, DI registration in `InfrastructureServiceCollectionExtensions.cs:61-66`, and generated migration/snapshot. FK `Warehouse.WorkspaceId -> Workspaces.Id` can cascade for the current model; revisit before non-cascading movement history is introduced. |
| Errors | `GlobalExceptionHandler.cs:31-68` maps product uniqueness conflict to ProblemDetails 409. | Add only a warehouse-name conflict mapping (title should not retain product wording). Do not expose raw SqlException. |
| Migration | `20260718112223_AddProducts.cs:10-54` is the current migration shape: table, workspace FK, tenant/list index, tenant unique index. | Generate migration with EF tool; commit generated migration, designer, and updated `ApplicationDbContextModelSnapshot.cs`, never hand-edit snapshot. |
| Frontend | `ProductsPage.tsx:16-151` combines TanStack list/mutations, Zod `safeParse`, invalidation, and client-side errors. `products-api.ts:4-11`, schema `:3-6`, types `:1-11` are feature-local. Router has a warehouse placeholder at `frontend/src/app/router/index.tsx:48-55`; sidebar already links `/warehouses` at `components/layout/Sidebar.tsx:25-35`. | Add `features/warehouses/{types,warehouses-api,warehouses-schema,pages/WarehousesPage}.tsx`, replace only the router placeholder. Key must be `['warehouses', user.id, user.workspace.id]`, equivalent to product key at `ProductsPage.tsx:16-37`. |
| Client tenancy/cache | `auth-store.ts:16-37` clears the entire QueryClient on logout or user/workspace change. Query default stale time is 30s (`query-client.ts:3-11`). | Keep identity/workspace in each tenant-data key; do not use `['warehouses']`. Existing session clearing covers sign-out/switch, but keying is still necessary for correct cache isolation. |
| Tests | Domain precedent: `ProductTests.cs:8-36`. SQL Server integration precedent: `ProductEndpointsTests.cs:26-105`, using real migrations/Testcontainers fixture (`AuthenticatedApiFixture.cs:14-91`). Workspace resolver/fail-closed behavior is proven in `WorkspaceMigrationAndTenancyTests.cs:38-124`. | Add warehouse domain tests and API integration tests in the matching files/namespaces. Existing fixture migrates before testing; test actual SQL unique-index conflict rather than mocking EF. |

## Required invariants

1. **Tenant boundary:** warehouse `WorkspaceId` is assigned from `ICurrentWorkspace` and never client supplied. Every read/mutation filters both `WorkspaceId` and entity ID. `ICurrentWorkspace == null` is 403; another workspace's ID is 404.
2. **Warehouse identity:** GUID, nonempty workspace/id/name, normalized trimmed name, UTC creation/archive instants. Choose one name collision contract and enforce it in the database. Recommended: unique `(WorkspaceId, Name)`, archived records included/reserved, as product SKU does (`ProductConfiguration.cs:18`).
3. **Lifecycle:** active list excludes archived; archive is idempotent. No hard delete. A later location/movement may safely reference archived warehouse history.
4. **Accounting safety:** no current quantity, available quantity, stock valuation, “inventory” totals, adjustments, transfer endpoint, or movement table in this slice. The UI must call these *warehouses*, not stock/inventory management.
5. **Database as concurrency authority:** duplicate warehouse creation may race. Do not rely on `AnyAsync` then insert; use unique index and catch provider-specific SQL Server duplicate key errors 2601/2627 as `WarehouseNameConflictException` -> HTTP 409 (the established product approach at `EfProductCatalog.cs:13-25`). Add/save should not leave the tracking context in a usable-but-pending-conflict state; the simplest local port has one context/request and immediately throws, matching product behavior.
6. **No future ledger shortcut:** when a later movement slice exists, it must be append-only, positive decimal quantity, one product and one warehouse per leg, transactionally write paired transfer legs/updates, and serially/concurrently protect the affected product+warehouse balance. Do not pre-create an editable `StockBalance` in this foundation merely to show zero.

## Concrete test/validation contract

**Unit (`WarehouseTests.cs`)**

* trims name; rejects empty ID/workspace/name and non-UTC timestamp;
* archive is idempotent and preserves its first timestamp.

**SQL Server API integration (`WarehouseEndpointsTests.cs`, `IClassFixture<AuthenticatedApiFixture>`)**

* authenticated create/list/archive/archive-again returns 201/200/204/204; archived is absent from active list;
* second canonical/normalized equivalent name in same workspace returns 409, including after archive, and exactly one row persists;
* same name in two independently registered workspaces succeeds; a second workspace receives 404 archiving the first warehouse;
* unauthenticated route returns 401 (framework auth) and a test arrangement with missing/ambiguous current workspace returns 403 if endpoint-level coverage is added;
* concurrent same-name posts yield one 201, one 409, one row. This is meaningful only because no precheck is used; current product implementation intentionally removed that precheck (`EfProductCatalog.cs:13-25`, `ProductEndpointsTests.cs:67-90`).

**Frontend validation:** Zod matches chosen max length/trim/min rule; query key includes current user/workspace; create/archive invalidate that exact key; page is protected through existing route hierarchy. There is no frontend test harness presently (`frontend/package.json:5-12` only typecheck/lint/build), so typecheck/lint/build/Prettier are the available targeted checks.

Run:

```bash
dotnet tool restore --tool-manifest backend/.config/dotnet-tools.json
dotnet test backend/InventoryFlow.sln --no-restore
dotnet format backend/InventoryFlow.sln --verify-no-changes --no-restore
cd frontend && bun run typecheck && bun run lint && bun run build && bunx prettier --check .
```

Current baseline was validated: `dotnet test backend/InventoryFlow.sln --no-restore` passed **15 unit + 15 Testcontainers/SQL Server integration** tests; all four frontend commands passed. Integration tests require a usable Docker/Testcontainers environment (`AuthenticatedApiFixture.cs:20-68`).

## Scope boundary and follow-on design

**Do now:** warehouse catalog only. It creates a safe foreign-key target and user-visible operating structure with minimal new concepts.

**Explicitly defer:** warehouse address/code/default warehouse, sub-locations/bins, product stocking, initial counts, adjustments, receiving, transfers, reservations, valuation, suppliers/orders, authorization roles beyond current single Owner.

**Next accounting-capable slice (not part of this task):** a transactionally posted immutable stock movement/ledger *after* warehouse exists. Decide whether movements are signed deltas with an external counterpart or explicit source/destination legs; enforce that a transfer does not alter workspace total, product and warehouses all belong to one workspace, quantities are nonzero decimal with an agreed precision, and outgoing operations cannot overdraw (unless negative inventory is explicitly a product policy). Its concurrency design needs an approved locking/optimistic-concurrency decision and real competing-write integration coverage; product-style DB uniqueness alone is insufficient for stock.

## Review findings

* **High (if scope expands to stock):** no current entity or persistence mechanism represents an immutable ledger, quantity precision, balance, paired transfers, or transaction/locking. Implementing `onHand` as an editable field on `Product`/`Warehouse` would lose auditability and allow lost updates. Evidence: only `Products`, workspace and auth DbSets exist in `ApplicationDbContext.cs:21-24`; search finds no warehouse/location/movement domain code.
* **Medium:** `CurrentWorkspaceResolver.cs:15-22` supports only exactly one Owner membership. This is intentional and documented (`backend/README.md:11-15`), but a future warehouse/member authorization/switching feature cannot assume multiple workspace selection exists.
* **Medium:** product’s unique index relies on the configured SQL Server collation for case semantics while the domain uppercases SKU. A warehouse name uniqueness contract must either deliberately use the same database collation behavior or persist a canonical name; do not claim collation-independent case-insensitive uniqueness without a canonical field.
* **Low:** current React has no test runner; UI behavior is compile/lint validated only. Add a test stack only if that is separately approved; do not make it a prerequisite for the narrow slice.

## Compact implementation-ready meta-prompt

> Implement the **workspace-scoped warehouse catalog** vertical slice, replacing the protected `/warehouses` placeholder. Mirror the established Product clean-architecture pattern: domain `Warehouse` with validated trimmed name, UTC timestamps, idempotent archive; application commands/queries/validator/port; SQL Server EF adapter/configuration/migration; and authorized controller endpoints `POST/GET/DELETE /api/warehouses`. Resolve workspace exclusively through `ICurrentWorkspace`; do not accept workspace IDs from the browser; scope find/list by workspace and return 403 when no current workspace and 404 for cross-workspace IDs. Add DB-enforced unique `(WorkspaceId, Name)` and translate SQL Server 2601/2627 to a typed 409 conflict—no read-then-write uniqueness precheck. Keep archived names reserved and exclude archived rows from active list. Register the adapter and DbSet, generate/update EF migration artifacts, and add SQL Server/Testcontainers lifecycle, tenancy, same-name cross-workspace, archived-name conflict, and concurrent-create tests plus domain invariant tests. Add a feature-local React API/types/Zod/page using a `['warehouses', userId, workspaceId]` query key and exact-key invalidation; existing auth cache clearing remains in effect. Do **not** add locations, stock quantities, movement/transfer/adjustment endpoints, or balances. Validate backend tests/format and frontend typecheck/lint/build/Prettier. Stop/escalate before adding inventory accounting or choosing a negative-stock/transfer/locking policy; those are intentionally out of scope.

## Resolved questions / assumptions

* The repository head is `034b6b0 feat(catalog): add workspace-scoped products`; branch name `feature/inventory/warehouse-foundation` exists but has no warehouse implementation beyond that shared head.
* “Warehouse” is the recommended first physical-inventory concept; locations are deferred because their only useful parent is a warehouse.
* Name-only warehouses are sufficient for the narrow catalog. Code/address/default selection are product decisions, not inferred requirements.
* Current single-owner workspace model is the sole authorization model. No role changes are required for warehouse catalog parity.