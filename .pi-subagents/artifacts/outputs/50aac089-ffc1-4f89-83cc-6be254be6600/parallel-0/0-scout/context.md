# Code Context

## Files Retrieved
1. `backend/src/InventoryFlow.Domain/Entities/WorkspaceMember.cs` (lines 7-29) - membership construction hard-restricts roles to Owner.
2. `backend/src/InventoryFlow.Domain/Entities/WorkspaceMemberRole.cs` (lines 1-8) - the only defined role is `Owner`.
3. `backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs` (lines 10-24) - request tenancy resolves exactly one owner membership and returns null for zero/multiple matches.
4. `backend/src/InventoryFlow.Infrastructure/Authentication/IdentityAuthenticationService.cs` (lines 28-43, 125-140) - registration provisions one personal workspace/owner membership; login/session selects exactly one owner workspace.
5. `frontend/src/components/layout/Topbar.tsx` (lines 105-125) - UI only displays the session's one workspace; it exposes no switcher.
6. `frontend/src/app/router/index.tsx` (lines 35-85) - `/reports` and `/users` are explicit placeholder pages, while operational routes are implemented.
7. `frontend/src/features/dashboard/pages/DashboardPage.tsx` (lines 32-105, 181-230) - dashboard labels itself real-time but all metrics, revenue chart, warehouse coverage, and activity are hard-coded.
8. `frontend/src/features/{products,warehouses,inventory}/*-api.ts` (products lines 1-11; warehouses lines 1-8; inventory lines 1-26) - existing authenticated list endpoints sufficient for a small real inventory snapshot.
9. `backend/src/InventoryFlow.Api/Controllers/InventoryController.cs` (lines 46-56) - balances are a workspace-derived, authenticated read endpoint.
10. `frontend/src/features/auth/auth-store.ts` (lines 17-34) - React Query is cleared on logout and identity/workspace change, supporting safe workspace-scoped dashboard keys.
11. `backend/tests/InventoryFlow.IntegrationTests/Api/AuthenticatedApiFixture.cs` (lines 12-46) - integration suite depends on SQL Server Testcontainers/Docker.
12. `README.md` (lines 125-139) - documents a branch/PR process and labels reporting, Redis, Hangfire, Docker Compose, and CI/CD as upcoming; no tracked delivery configuration was found by `git ls-files`.

## Key Code

### Current comparison

| Area | Current state | Consequence |
|---|---|---|
| Roles/invitations | **Not started.** Only `Owner` exists, and the constructor rejects every other role. There is no invitation entity, token, API/controller, email delivery, member listing, or permission policy. | This is a cross-cutting identity/tenancy redesign, not the smallest next slice. |
| Workspace switching | **Not started.** A session carries one workspace, selected only if the user has exactly one owner membership; the resolver follows the same rule. Topbar only renders its name. | Supporting multiple workspaces requires an explicit active-workspace contract (likely token/session claim or validated request selector), cache invalidation, and authorization changes. Do not bolt a UI switcher onto the current API. |
| Reporting/dashboard | **Dashboard shell only.** `/reports` is placeholder. Dashboard's claimed real-time values are static (`"1,248"`, revenue, activity, warehouse distribution), not API-backed. | **High product-integrity finding:** current dashboard can mislead an authenticated operator about inventory and sales. |
| CI/CD/Docker | **Not started.** No tracked `.github` workflow, Dockerfile, compose file, or deployment manifest. The existing integration tests require a Docker-hosted SQL Server container. | CI/Docker is valuable enablement but larger than a focused product slice and should first define deployment/secrets/migration policy. |
| Caching/jobs | **Only browser query caching exists.** TanStack Query has a 30-second stale time; session transitions clear it. No Redis package/configuration, cache abstraction, background service, Hangfire/Quartz package, job persistence, or outbox exists. | Introducing distributed cache/jobs now creates invalidation, retry, and durability decisions with no immediate user-facing requirement. |

### Relevant tenancy constraints

```csharp
if (role != WorkspaceMemberRole.Owner)
    throw new DomainException("Only the Owner workspace role is supported.");
```

`CurrentWorkspaceResolver` filters `member.Role == Owner`, reads at most two memberships, and only accepts exactly one (`CurrentWorkspaceResolver.cs:18-22`). Authentication repeats this selection (`IdentityAuthenticationService.cs:125-131`). Thus invitations/memberships/switching must update all three semantic points together, plus the issued user/session representation and frontend cache lifecycle.

### Recommended next smallest valuable safe slice

**Implement a real, read-only workspace dashboard snapshot; do not build reporting, roles/invitations, switching, CI/CD, Redis, or jobs in this slice.**

Replace the fabricated dashboard with three authenticated React Query reads already available:

* `listProducts()` → active product count;
* `listWarehouses()` → active warehouse count;
* `listInventoryBalances({})` → total on-hand quantity, count of stocked SKU/warehouse balances, and optionally per-warehouse on-hand distribution.

Use query keys shaped like existing pages: `["dashboard", "snapshot", userId, workspaceId]`. The auth store already clears every query on logout or identity/workspace transition, so no new cache-lifecycle mechanism is needed. Include loading, error, and zero-state UI; remove/withhold cards that cannot be truthful.

**Deliberate scope boundary:** do not display inventory value, revenue/today's sales, low-stock alerts, trends, export, or recent unified activity. The current model has no product cost, stock threshold, sales price/date aggregation endpoint, report export contract, or generic movement-history endpoint. A later reporting slice should introduce explicit backend projections/DTOs rather than aggregate client-side operational lists.

### Local integration points

1. First change: `frontend/src/features/dashboard/pages/DashboardPage.tsx` — remove module-level fixtures and add `useQuery` calls plus honest metric/coverage rendering.
2. Reuse `frontend/src/features/products/products-api.ts`, `frontend/src/features/warehouses/warehouses-api.ts`, and `frontend/src/features/inventory/inventory-api.ts`; no API or migration change is necessary.
3. Mirror key construction and enabled behavior from `frontend/src/features/inventory/pages/InventoryPage.tsx` (lines 17-79), using current `useAuthStore` user identity.
4. Validate dashboard access with an authenticated workspace containing no data and one containing multiple warehouses/balances. Existing `auth-store.ts:21-34` provides cache isolation; test it directly if frontend test tooling is added.

## Architecture

The completed operational flow is browser → Axios bearer/refresh client → `[Authorize]` controller → `ICurrentWorkspace` lookup → MediatR command/query → workspace-scoped EF services. Products, warehouses, and balances are already returned from tenant-derived endpoints, and frontend pages key their server-state cache by user/workspace. A dashboard snapshot can therefore be a frontend-only composition without weakening tenancy or introducing durable derived data.

Roles, invitations, and switching sit below every operational controller because workspace selection is currently an invariant, not a user-selectable field. Reporting needs new domain semantics for money and thresholds. Redis/jobs and CI/Docker are horizontal platform work; neither is required to make the existing operational data visible truthfully.

## Start Here

Open `frontend/src/features/dashboard/pages/DashboardPage.tsx`: it has the concrete user-facing defect and can be converted into a truthful, isolated dashboard entirely through existing APIs and auth/cache conventions.

## Review Findings

1. **High — `frontend/src/features/dashboard/pages/DashboardPage.tsx:40-96, 181-230`:** authenticated dashboard presents fabricated inventory, sales/revenue, warehouse, and activity data while describing itself as real-time (`lines 101-104`). Remove or replace it before representing the app as operational.
2. **Medium — `backend/src/InventoryFlow.Domain/Entities/WorkspaceMember.cs:10-15`, `.../WorkspaceMemberRole.cs:4-8`, `.../Tenancy/CurrentWorkspaceResolver.cs:18-22`:** collaboration features cannot be incrementally added; non-owner roles are rejected and a user with multiple owner memberships is denied a current workspace. Treat roles/invitations/switching as one deliberate authorization/active-workspace slice.
3. **Medium — `frontend/src/app/router/index.tsx:77-79`:** reports and user management remain placeholders; no reporting contract should be inferred from the dashboard shell.
4. **Medium — repository root (absence confirmed by `git ls-files` delivery/ops scan):** no CI workflow, Dockerfile, Compose manifest, or deployment configuration exists, while `AuthenticatedApiFixture.cs:22-41` requires Docker/Testcontainers. Automated integration validation is not presently runnable in a generic CI agent without first provisioning Docker.
5. **Low — `README.md:137-139`:** roadmap is stale: it lists authentication/catalog/inventory/purchases/sales as upcoming though those vertical slices are implemented on current `main`.

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "Concrete severity-tagged findings and exact file/line references are provided in Review Findings and Files Retrieved."
    }
  ],
  "changedFiles": [],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "git status --short && git log --oneline -8",
      "result": "passed",
      "summary": "Confirmed current HEAD contains the atomic warehouse-transfer commit; only untracked .pi-subagents artifacts were present."
    },
    {
      "command": "git ls-files | grep -Ei '(^\\.github/|(^|/)(dockerfile|docker-compose|compose\\.(ya?ml))$|\\.ya?ml$)'",
      "result": "passed",
      "summary": "Returned no tracked CI, Dockerfile, Compose, or YAML delivery configuration."
    },
    {
      "command": "targeted grep/read/nl inspection of tenancy, auth, router, dashboard, API clients, tests, and README",
      "result": "passed",
      "summary": "Established current capability boundaries and frontend-only dashboard integration points."
    }
  ],
  "validationOutput": [
    "No code was edited or test suite run; this was a read-only scout task.",
    "Current operational endpoints and frontend client functions support a real dashboard snapshot without backend/schema changes."
  ],
  "residualRisks": [
    "The proposed frontend-composed snapshot is eventually consistent across its three independent reads; it must not claim a single transactional reporting instant.",
    "Inventory value, low-stock alerts, sales revenue/trends, export, and unified activity require explicit new domain fields/projection endpoints and are excluded.",
    "Roles/invitations/switching remains a cross-cutting authorization design project; adding it piecemeal would break the exactly-one-owner tenancy invariant.",
    "Integration tests require Docker/Testcontainers, and no CI/Docker configuration currently provisions that environment."
  ],
  "noStagedFiles": true,
  "diffSummary": "No repository source changes; read-only findings artifact only.",
  "reviewFindings": [
    "high: frontend/src/features/dashboard/pages/DashboardPage.tsx:40-96,181-230 renders fabricated operational data as a real-time dashboard.",
    "medium: backend/src/InventoryFlow.Domain/Entities/WorkspaceMember.cs:10-15 and Infrastructure/Tenancy/CurrentWorkspaceResolver.cs:18-22 prevent non-owner membership and multi-workspace selection.",
    "medium: frontend/src/app/router/index.tsx:77-79 leaves reports/users as placeholders.",
    "medium: no tracked CI/Docker delivery configuration; backend/tests/InventoryFlow.IntegrationTests/Api/AuthenticatedApiFixture.cs:22-41 requires Docker Testcontainers.",
    "low: README.md:137-139 roadmap is outdated."
  ],
  "manualNotes": "Recommended next slice: frontend-only real workspace dashboard snapshot from products, warehouses, and balances; remove unavailable money/alert/report claims."
}
```