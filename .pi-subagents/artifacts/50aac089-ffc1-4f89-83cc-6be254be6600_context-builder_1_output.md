# Post-transfer implementation context

## Recommendation: read-only **Inventory Availability Report**

Replace the authenticated `/reports` placeholder with a workspace-scoped, current-on-hand report. This is the smallest high-value post-transfer slice because every operational write path (manual receipt/issue, purchase receipt, sales fulfillment, transfer) already materializes `InventoryBalances`; it needs **no schema, domain, application, controller, or new API work**. It gives operators a cross-warehouse view that the existing Inventory page only exposes incidentally, while avoiding unsupported financial or historical claims.

Do not turn the static dashboard into this slice: it contains fabricated financial, sales, low-stock, pending-transfer, and activity data. No pricing, sales amount, reorder threshold, transfer state, movement feed, date-range history, roles/invitations, caching/jobs, Docker, CI/CD, CSV export, or dashboard rewrite belongs here.

## Evidence and exact integration points

| Area | Evidence | Required use / implication |
|---|---|---|
| Existing data/API | `backend/src/InventoryFlow.Api/Controllers/InventoryController.cs:46-56` exposes authenticated `GET /api/inventory/balances?warehouseId=&productId=`. The controller derives the workspace from `ICurrentWorkspace`, never client input. | Reuse this endpoint exactly. The report must not receive a workspace ID or invent `/api/reports`. |
| Query semantics | `backend/src/InventoryFlow.Infrastructure/Inventory/EfInventoryLedger.cs:30-37` filters `InventoryBalances` by workspace, applies optional warehouse/product IDs, and orders warehouse then product. `InventoryBalanceResponse` is only `{ warehouseId, productId, quantity }` in `...Application/Features/Inventory/InventoryModels.cs:12-17`. | The report is current on-hand quantities only; joins to display names occur client-side from active catalog queries. Do not claim movement history, valuation, sales revenue, or low-stock status. |
| Durable source | `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/InventoryBalanceConfiguration.cs:10-20` configures `decimal(18,4)` current balances keyed by workspace/warehouse/product. `InventoryBalance.cs:7-41` prevents negative balances. | Format quantities with max four fractional places; preserve zero if returned. |
| Operational coverage | `PurchaseReceipts`, `SalesFulfillments`, and `WarehouseTransfers` are persisted alongside `InventoryMovements` and `InventoryBalances` in `...Persistence/ApplicationDbContext.cs:19-39`; transfer service atomically applies equal/opposite balances (`...Transfers/EfWarehouseTransferService.cs:66-84`). | The report automatically includes all posted workflows because it reads balances, not per-feature histories. |
| Existing client contract | `frontend/src/features/inventory/inventory-api.ts:8-16` already exports `listInventoryBalances(filters)`. `types.ts:1-5` is the exact `InventoryBalance` type. | Import this API/type; do not duplicate an Axios client or add an API endpoint. |
| Name resolution | `frontend/src/features/inventory/pages/InventoryPage.tsx:68-86` queries products, warehouses, and balances with `enabled: user !== null`; `:254-276` maps IDs to names and renders an empty/error/loading state. | Mirror this proven query and display pattern. Fetch `listProducts` and `listWarehouses`; use maps only for labels. A missing name must fall back to the ID, as existing operational pages do. |
| Tenant/query cache invariant | Inventory query keys include `userId` and `workspaceId` (`InventoryPage.tsx:25-34,57-85`); `frontend/src/features/auth/auth-store.ts:21-34` clears TanStack Query cache whenever user/workspace changes or session clears. | The new report key must include both values and use `enabled: user !== null`. Use e.g. `["reports", "inventory-availability", userId, workspaceId, warehouseId, productId]`; do not use a global `['reports']` key. |
| UI route | `/reports` is a protected placeholder at `frontend/src/app/router/index.tsx:35-79`; Sidebar already links to it at `frontend/src/components/layout/Sidebar.tsx:19-32`. | Replace only the route with lazy `@/features/reports/pages/ReportsPage`. Sidebar needs no change. |
| Design pattern | `InventoryPage.tsx:52-55` uses `Intl.NumberFormat`; `:236-276` uses PageHeader/Card/list and conventional loading/error/empty states. `PurchasesPage.tsx` and `SalesPage.tsx` use the same workspace-aware query pattern. | Implement one report page using `PageHeader`, `Card`, native select filters, and a responsive list/table. Keep filters client state; selected IDs are passed as optional request query parameters. |
| Tests | `backend/tests/InventoryFlow.IntegrationTests/Api/InventoryEndpointsTests.cs:29-56` already exercises persisted balances and `GET /api/inventory/balances` against SQL Server. Fixture uses a migrated SQL Server Testcontainer (`AuthenticatedApiFixture.cs:12-46`). Frontend has no test script/framework in `frontend/package.json:5-11`. | No backend test or test dependency is justified when no backend contract changes. Manually verify filter behavior and tenant-safe response through the existing endpoint; validate the frontend with typecheck/lint/build. |

## Minimal UI/data contract

1. Add `frontend/src/features/reports/pages/ReportsPage.tsx` and replace the router placeholder only.
2. Title: **Reports**; description should explicitly say **current on-hand inventory by warehouse and product**.
3. Query active products, active warehouses, and balances. Select controls: optional Warehouse and Product, with “All …” default values. `listInventoryBalances({ warehouseId: selected || undefined, productId: selected || undefined })` is the sole report data request.
4. Render a compact responsive report with product (name and SKU), warehouse, and on-hand quantity. Existing catalog endpoints only list active entities, so the fallback ID is essential for a historical balance whose catalog item is unavailable in a transient/race case.
5. Include loading, API-error (`role="alert"`), and zero-result states. Format numbers using existing `Intl.NumberFormat(undefined, { maximumFractionDigits: 4 })` behavior.
6. No mutations, idempotency keys, invalidation, aggregation, chart, or export action is necessary. Balances are refreshed on mount/filter change; operational pages already invalidate the common `['inventory','balances',userId,workspaceId]` prefix after a successful write.

## Risks / non-negotiable boundaries

- **High — semantic integrity:** Dashboard constants at `frontend/src/features/dashboard/pages/DashboardPage.tsx:40-96,180-260` are mock figures. Product data has no price/cost; sales fulfillments have no monetary amount; no reorder thresholds or transfer lifecycle exist. Displaying “inventory value,” “today’s sales,” “low stock,” “pending transfers,” revenue charts, or fabricated recent activity as live report data would be false.
- **Medium — scope duplication:** `InventoryPage.tsx` already has a balance list. The report’s differentiator is a read-only, dedicated cross-warehouse operational view; do not refactor/move the inventory posting form or redesign dashboard in this slice.
- **Medium — identifier-only API:** Balance responses intentionally omit product/warehouse names. Resolve names from existing catalog lists and retain ID fallback; do not add joins/DTO fields just for convenience.
- **Medium — authorization:** Preserve the established user/workspace-specific query key plus `enabled: user !== null`; never infer tenancy from route/filter state.
- **Low — frontend automated coverage gap:** There is no frontend test runner configured. Do not introduce Vitest/RTL solely for this small page unless separately approved.

## Meta-prompt handoff

**Goal**: Implement the minimal authenticated Inventory Availability Report: turn `/reports` into a lazy-loaded, read-only page that lists current workspace balances by product and warehouse, with optional warehouse/product filters.

**Context/evidence**: Reuse `GET /api/inventory/balances` and `listInventoryBalances` exactly (`backend/src/InventoryFlow.Api/Controllers/InventoryController.cs:46-56`; `frontend/src/features/inventory/inventory-api.ts:8-16`). Its data is `{ warehouseId, productId, quantity }`, workspace-scoped in persistence (`EfInventoryLedger.cs:30-37`). Follow `InventoryPage.tsx:57-86,236-276` for auth-scoped queries, maps, state display, and number formatting. Route seam is `frontend/src/app/router/index.tsx:77`. Query keys must include both user and workspace, per `InventoryPage.tsx:25-34` and auth cache clearing in `auth-store.ts:21-34`.

**Success criteria**:
- Authenticated `/reports` loads a page, not the placeholder.
- It queries products, warehouses, and current balances only after a user exists; all keys contain `userId` and `workspaceId`.
- Optional filters issue the existing balance request with omitted (`undefined`) empty parameters.
- Every row has product, SKU where known, warehouse, and formatted on-hand quantity; loading/error/empty states are accessible.
- No server, migration, API, domain, or dependency changes are made.

**Hard constraints**: No financial metrics/value/revenue, threshold/low-stock calculations, transaction/movement history, transfer statuses, charts, CSV/PDF export, dashboard rewrite, roles/workspace switching, cache/jobs/deployment work, or client-supplied workspace ID. Do not change existing operational screens.

**Suggested approach**: Create `features/reports/pages/ReportsPage.tsx`, copying only the established query/error/list structure. Define a local `reportKey` that contains report name, user ID, workspace ID, and filters. Build two ID→entity maps from catalog data; render fallback IDs. Replace the route’s placeholder with a lazy import. No Sidebar edit.

**Validation**:
- `cd frontend && bun run typecheck && bun run lint && bun run build`.
- Manual/API smoke: authenticate, visit `/reports`, create product/warehouses and a receipt/transfer through existing pages or API, confirm rows and each filter; sign out/sign in as another user and confirm no prior workspace data appears. Existing SQL Server endpoint coverage is `InventoryEndpointsTests.cs:29-56`; no new backend test is needed absent an API change.

**Stop/escalation rules**: Stop after the route/page and frontend validation. Escalate before adding an endpoint, schema/migration, price/threshold model, export, chart, or test framework. If catalog names cannot be found, render IDs rather than expanding the API.

**Resolved assumptions**: “Report” means a live current availability view, not financial reporting. Active product/warehouse list endpoints are appropriate for labels; balance endpoint remains the authoritative quantity source.