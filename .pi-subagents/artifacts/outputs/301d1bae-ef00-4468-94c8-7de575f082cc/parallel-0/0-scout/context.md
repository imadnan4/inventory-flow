# Code Context

## Files Retrieved
1. `backend/src/InventoryFlow.Domain/Entities/WorkspaceMember.cs` (lines 7-30) - membership domain invariant currently permits only `Owner`.
2. `backend/src/InventoryFlow.Domain/Entities/WorkspaceMemberRole.cs` (lines 1-9) - the entire role vocabulary is a single `Owner` value.
3. `backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs` (lines 10-23) - every business request derives tenancy from the bearer user and exactly one owner membership.
4. `backend/src/InventoryFlow.Infrastructure/Authentication/IdentityAuthenticationService.cs` (lines 25-43, 47-88, 107-155) - registration provisions a personal workspace; login/refresh/me select exactly one owner workspace and return it in the session.
5. `backend/src/InventoryFlow.Application/Features/Authentication/AuthenticationModels.cs` (lines 3-23) - public auth DTO contains one `workspace`, without role or workspace list.
6. `backend/src/InventoryFlow.Api/Controllers/AuthController.cs` (lines 14-72) - auth/session endpoints and the authorized `/api/auth/me` contract.
7. `backend/src/InventoryFlow.Api/Controllers/ProductsController.cs` (lines 9-49) - representative protected controller: resolves workspace server-side, never accepts a client workspace ID. The other seven inventory controllers use this same pattern.
8. `backend/src/InventoryFlow.Infrastructure/Persistence/ApplicationDbContext.cs` (lines 9-58) and `Configurations/WorkspaceMemberConfiguration.cs` (lines 9-24) - existing persistence sets and unique `(WorkspaceId, UserId)` membership constraint.
9. `backend/src/InventoryFlow.Infrastructure/Persistence/Migrations/20260718105902_AddWorkspaces.cs` (lines 10-87) - workspace migration backfilled exactly one Owner workspace for legacy users.
10. `backend/tests/InventoryFlow.IntegrationTests/Api/WorkspaceMigrationAndTenancyTests.cs` (lines 24-129) - current integration contract expressly asserts one owner membership and fails closed when it is ambiguous.
11. `frontend/src/features/auth/types.ts` (lines 1-16), `auth-store.ts` (lines 9-37), and `lib/api-client.ts` (lines 7-66) - session shape, query cache clearing, bearer injection, and refresh propagation.
12. `frontend/src/components/layout/Topbar.tsx` (lines 100-126) - only display of `user.workspace`; no workspace selector or collaboration route exists.

## Key Code

### Current tenancy/auth contract
* A registered user gets a new `Workspace` and immutable `WorkspaceMember(... Owner ...)` atomically (`IdentityAuthenticationService.cs:25-43`).
* `GetWorkspaceAsync` and `CurrentWorkspaceResolver.GetAsync` query only `member.Role == Owner`, take two results, and return a result only when the count is exactly one (`IdentityAuthenticationService.cs:125-132`; `CurrentWorkspaceResolver.cs:16-22`). Thus inviting a second role would make the user unable to resolve any workspace unless those paths change; adding a second Owner already intentionally returns 403 on inventory APIs.
* Access JWT has identity/display claims only; no workspace or role claim (`backend/src/InventoryFlow.Infrastructure/Authentication/JwtAccessTokenIssuer.cs:14-23`). Server-side resolver therefore remains the enforcement point on every request.
* Controllers resolve `ICurrentWorkspace` then pass only its ID to MediatR (`ProductsController.cs:18-47`). Catalog/ledger persistence methods also filter by passed `WorkspaceId`, so the client currently cannot choose another workspace.
* Browser session responses and `/api/auth/me` expose `{ user: { id, email, displayName, workspace: { id, name }}}`; refresh rotates only the token and reconstructs that same one-workspace response. Frontend query keys include `user.workspace.id`, and `setSession` clears the whole React Query cache if it changes (`auth-store.ts:21-30`).

### Smallest safe collaboration slice
Implement one vertical slice, retaining all inventory controllers and their commands unchanged:

1. **Membership roles:** expand `WorkspaceMemberRole` to `Owner` and `Member` (do not add a speculative hierarchy). Remove the constructor's Owner-only guard; retain IDs/UTC validation. Define permissions centrally: Owner can list/create/revoke invitations and see members; both Owner and Member can use existing inventory APIs. There is no role-based restriction today, so leaving membership-management endpoints guarded only by `ICurrentWorkspace` would be privilege escalation.
2. **Invitations:** add `WorkspaceInvitation` persistence with `Id`, `WorkspaceId`, normalized `Email`, `Role`, opaque-token **hash** (never raw token), `ExpiresAtUtc`, `AcceptedAtUtc`, `RevokedAtUtc`, `InvitedByUserId`, and a concurrency token/unique active-invitation rule. Add owner-only create/list/revoke endpoints and an authenticated accept endpoint. The accept transaction must: resolve token by hash, reject expired/revoked/accepted token, compare normalized invite email to the authenticated Identity email, and insert membership with the existing unique `(WorkspaceId, UserId)` constraint; duplicate/parallel accept is idempotent or returns a deterministic conflict. Smallest scope should invite **existing accounts only** and return/copy a token/link rather than introduce mail delivery or registration-token continuation.
3. **Switching:** change `ICurrentWorkspace`/`CurrentWorkspace` to include the resolved member role, and have `CurrentWorkspaceResolver` read a single explicit `X-Workspace-Id` GUID (or a documented equivalent) from the request. It must join membership by bearer user + requested workspace, accepting either role; missing/invalid/unrelated headers fail closed (403). Do not trust an arbitrary workspace ID from body/query. Add an authorized `GET /api/auth/workspaces` that returns the caller's memberships `{id,name,role}`. Add `POST /api/auth/workspaces/select` only if the product requires a server-persisted default; it is not needed for request-header switching and otherwise requires a new per-user active-workspace column.
4. **Session/UI:** revise frontend auth types so the active workspace includes role and the user has `workspaces[]`; preserve `user.workspace` as active workspace to minimize downstream changes. Put selector in `Topbar`; selecting a workspace must verify it is in `workspaces`, set active user state, clear query cache, and cause `apiClient` to attach `X-Workspace-Id`. On reload, select a deterministic first workspace (or persisted selection) before queries. Refresh/me must return the membership list or fetch it before enabling protected queries.

Likely change locations: new Domain invitation entity and EF config/DbSet/migration; `WorkspaceMember*`, current-workspace contracts/resolver, collaboration application feature/handlers, a `WorkspacesController` or AuthController methods, auth response/service/types, `auth-store.ts`, `api-client.ts`, `Topbar.tsx`, and controller/integration/unit tests. Existing catalog/ledger implementations need no workspace ID API changes if resolver remains authoritative.

## Architecture

`Axios request -> JWT bearer -> controller -> ICurrentWorkspace -> MediatR command/query -> EF service` is the tenancy chain. The resolver uses Identity's name-identifier claim, then membership DB rows; it does not rely on JWT workspace claims. Auth registration/login/refresh/me separately build the client session from the same owner-only selection. React Query caches are already partitioned by active workspace ID and the auth store already clears cache on workspace change, providing a safe UI switching seam once the active workspace is actually selectable and sent to the API.

## Data/security risks

* **Blocker / high:** Current owner-only resolver and auth selector become null for any user with multiple memberships (`CurrentWorkspaceResolver.cs:18-22`, `IdentityAuthenticationService.cs:125-132`). Roles/invites must not ship without replacing both selections.
* **Blocker / high:** A request header alone is an insecure tenant selector unless resolver checks bearer-user membership and role server-side. Never forward a frontend workspace ID into MediatR directly.
* **High:** Member invitations must be Owner-authorized independently of inventory authorization; otherwise any Member can escalate collaborators/roles.
* **High:** Plain invitation tokens in SQL/logs, no expiration/revocation, token acceptance by an email-mismatched logged-in user, or non-atomic acceptance permit account takeover/cross-user membership. Hash tokens and transact acceptance.
* **Medium:** Existing migration backfill semantics and integration test encode one personal owner workspace. Preserve it; do not add a uniqueness constraint on `UserId`, because multi-workspace membership is required. Update its ambiguous-owner assertion to explicit selection semantics.
* **Medium:** Every cached resource key is workspace-scoped today, but switching must still clear cache (already done by `setSession`) to prevent stale page content and in-flight requests from appearing under the new workspace.
* **Medium:** Existing refresh tokens are user/family scoped, not workspace scoped. This is compatible with header selection, but a removed membership must take effect on the next resolver call and workspace-list refresh; client state alone cannot authorize.

## Acceptance

* Owner creates a Member invitation for an existing account; a Member receives 403 for invitation create/list/revoke.
* Wrong user cannot accept an invite; expired, revoked, reused, malformed, and concurrent duplicate acceptance cannot create extra/incorrect membership; token storage is hashed.
* Accepted user gets two workspace memberships and can list/switch only those. Unknown/missing/malformed `X-Workspace-Id` returns 403; a user cannot read or mutate either inventory data or membership data in another workspace.
* Owner and Member each retain access to current inventory APIs in their selected workspace; returned records never cross workspace boundary. Role and invite actions have explicit controller/service authorization tests.
* Registration, login, refresh, `/api/auth/me`, and legacy workspace migration remain compatible: personal owner workspace still exists; session/workspace-list contracts correctly represent membership and active selection.
* UI selector changes workspace label, sends selection header, clears/refetches workspace-keyed data, and browser refresh restores a valid selected/default membership.

## Start Here

Open `backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs` first: it is the single current-workspace authorization choke point and exposes why adding more memberships presently fails closed. Then update the parallel owner-only session selector in `IdentityAuthenticationService.cs` before designing frontend selection.

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "Concrete tenancy/auth contracts, exact file paths and ranges, severity-tagged findings, smallest safe change slice, and acceptance scenarios are documented above."
    }
  ],
  "changedFiles": [],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "git status --short && git branch --show-current; targeted repository reads/searches",
      "result": "passed",
      "summary": "Inspected current feature branch; no source files edited. Only runtime .pi-subagents artifact directory is untracked."
    }
  ],
  "validationOutput": [
    "Review-only scouting completed; no test suite run because no implementation changed."
  ],
  "residualRisks": [
    "Product policy is unspecified for Member inventory permissions and invite delivery/new-account onboarding; this scout recommends existing-account invitations and Owner/Member only as the smallest safe scope.",
    "Header-based selection requires every protected controller to continue using ICurrentWorkspace; future endpoints that accept WorkspaceId must not bypass it."
  ],
  "noStagedFiles": true,
  "diffSummary": "No source diff; findings written only to the required scout artifact.",
  "reviewFindings": [
    "blocker/high: backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs:18-22 resolves exactly one Owner membership, so collaboration/multiple workspaces currently fail closed.",
    "blocker/high: backend/src/InventoryFlow.Infrastructure/Authentication/IdentityAuthenticationService.cs:125-132 has the same exact-one-Owner assumption for login, refresh, and /me.",
    "high: no invitation entity, endpoint, token lifecycle, or role authorization exists; adding UI-only collaboration would not create secure memberships."
  ],
  "manualNotes": "No edits were made. The working tree had pre-existing untracked .pi-subagents runtime artifacts."
}
```