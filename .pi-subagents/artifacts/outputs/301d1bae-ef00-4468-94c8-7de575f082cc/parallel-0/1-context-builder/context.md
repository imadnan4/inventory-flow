# Implementation context: safe first collaboration slice

## Recommendation and boundary

Implement **authenticated, email-bound invitations + two membership roles + server-backed active-workspace switching** as one security slice. Do **not** attempt general per-feature RBAC, workspace creation/deletion, owner transfer, member removal, email delivery, invite links for unauthenticated users, or a Redis cache in this slice.

The smallest coherent behavior is:

1. Registration continues to make exactly one personal `Owner` workspace.
2. Add `Member` alongside `Owner`. Existing operational endpoints are available to an active **Owner or Member**; only workspace-membership administration is Owner-only. This preserves current operation authorization while making the new role meaningful without an unapproved permission matrix.
3. An Owner creates a time-limited, pending invitation for an email and `Member` role; can list/revoke pending invitations. No outbound email is sent. A signed-in user may list and accept only pending, unexpired invitations whose normalized email equals their Identity email. Acceptance atomically creates the membership and consumes the invitation. It does not automatically switch workspace.
4. The authenticated user/session response exposes the active workspace plus all memberships (workspace id/name/role). `POST /api/auth/workspaces/{workspaceId}/switch` verifies membership, persists the selection to the refresh-token session, and returns a newly issued access token/session. Refresh preserves that selected workspace.
5. Every protected request derives the active workspace from a **signed JWT workspace claim** and re-checks `(userId, workspaceId)` membership in SQL. Never trust a client workspace id/header alone. The active workspace must be current membership at refresh/switch time and request time.

This is the safest first slice because the current production boundary assumes exactly one Owner workspace; simply adding membership/invites without replacing that resolver would leave collaborators unable to use the operational workflow. The next feature can layer a reviewed permission matrix on top of the role field.

## Current state and concrete evidence

| Area | Files / lines | Finding / consequence |
|---|---|---|
| Current role model | `backend/src/InventoryFlow.Domain/Entities/WorkspaceMemberRole.cs:4-8`; `WorkspaceMember.cs:9-19` | Only `Owner` exists and the domain constructor rejects every other role. It must be expanded before invitation acceptance can produce a collaborator membership. |
| Current tenancy boundary | `backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs:11-23` | It joins only `member.Role == Owner`, takes two rows, and returns null unless exactly one. A Member would receive 403 from every existing controller; multiple workspaces cannot be active/switchable. |
| Controller pattern | `backend/src/InventoryFlow.Api/Controllers/ProductsController.cs:10-49`; same pattern in `InventoryController.cs:10-61`, purchase/sales/transfers/suppliers/warehouses controllers | Controllers are `[Authorize]`, call `ICurrentWorkspace.GetAsync`, return `Forbid()` when null, and pass server workspace id into application commands. Preserve this pattern; do not add a caller-controlled workspace id to operational requests. |
| Operational scoping | `backend/src/InventoryFlow.Infrastructure/Products/EfProductCatalog.cs:29-57` and equivalent services | Persistence consistently scopes by `WorkspaceId`; existing rows and cache keys already expect a selected workspace. |
| Auth provisioning | `backend/src/InventoryFlow.Infrastructure/Authentication/IdentityAuthenticationService.cs:23-45` | Registration atomically creates user, personal workspace, Owner membership, refresh token, and session. Keep this transaction and seed its refresh token with the personal active workspace. |
| Auth workspace lookup | `IdentityAuthenticationService.cs:124-134` | Login/refresh/`me` currently query only unique Owner workspace. This must be split into (a) active workspace validation and (b) all memberships projection. Do not continue using uniqueness of all memberships as a tenancy check. |
| Token contents | `backend/src/InventoryFlow.Infrastructure/Authentication/JwtAccessTokenIssuer.cs:15-22` | JWT has subject/email/display name only, no workspace claim. A signed active-workspace claim is required for request resolution. |
| Refresh session | `backend/src/InventoryFlow.Domain/Entities/RefreshToken.cs:13-97`; `.../Configurations/RefreshTokenConfiguration.cs:13-39`; `IdentityAuthenticationService.cs:60-93` | Refresh tokens have user/family/hash/lifetime/revocation only. Add a required `ActiveWorkspaceId` FK and carry it through rotation; refresh already runs serializably and must verify membership before issuing. |
| Browser session/cache | `frontend/src/features/auth/types.ts:1-15`; `auth-store.ts:17-36`; `lib/api-client.ts:18-77` | Session is memory-only; access token refresh is single-flight and epochs prevent stale refresh overwrites. `setSession` clears React Query only when user or `user.workspace.id` changes. Switching should use this same session setter so all workspace-scoped queries are cleared before fetching the new workspace. |
| Existing query isolation | `frontend/src/features/inventory/inventory-queries.ts:1-19`; pages such as `products/pages/ProductsPage.tsx:15-34`, `purchases/pages/PurchasesPage.tsx:19-75` | Query keys include both user and workspace; this is correct and must be retained. A switch must not directly mutate `user.workspace` outside `setSession`. |
| Existing test infrastructure | `backend/tests/InventoryFlow.IntegrationTests/Api/AuthenticatedApiFixture.cs:14-91`; `ProductEndpointsTests.cs:17-112` | Real SQL Server Testcontainers fixture and endpoint-level two-user isolation/concurrency pattern already exist. Reuse it for lifecycle, authorization, and switching tests. |
| Existing migration/resolver proof | `WorkspaceMigrationAndTenancyTests.cs:30-147` | SQL migration backfill plus resolver isolation/ambiguity tests exist, but they codify Owner-only/unique behavior and must be updated for active signed workspace semantics. |

## Proposed implementation shape

### Domain and SQL migration

- Extend `WorkspaceMemberRole` to exactly `Owner = 1, Member = 2`; change `WorkspaceMember` validation to permit defined roles. Keep a membership immutable in this slice (no role change/remove endpoints).
- Add a `WorkspaceInvitation` aggregate/table: id, workspace id, normalized email, invited role (only `Member` accepted by this P0), status (`Pending`, `Accepted`, `Revoked`, `Expired`), created/expiry/accepted/revoked timestamps, invited-by user id, and accepted-by user id. Normalize by trim + upper invariant, validate email with FluentValidation at API/application edge, and do not return raw untrusted identity data.
- Index invitations for owner list and recipient lookup: `(WorkspaceId, Status, CreatedAtUtc)` and `(NormalizedEmail, Status, ExpiresAtUtc)`. Enforce one pending invitation for `(WorkspaceId, NormalizedEmail)` with a SQL Server filtered unique index on `Status = 'Pending'`. In a serializable transaction, transition an expired pending row to `Expired` before creating a replacement; map a unique-index race to a deterministic 409 rather than creating duplicate memberships.
- Preserve `WorkspaceMembers` unique `(WorkspaceId, UserId)` (`WorkspaceMemberConfiguration.cs:18`). Invitation acceptance must treat existing membership as idempotent/no-op only when the same recipient already belongs; do not create a duplicate or disclose a different workspace’s membership.
- Add required `RefreshToken.ActiveWorkspaceId` with FK to `Workspaces` (use `DeleteBehavior.Restrict`/no cascade). Migration backfill should derive each token's sole Owner membership; migration must fail/handle explicitly rather than silently choose on legacy ambiguity. New registration assigns its personal workspace. Generate the EF migration/snapshot and SQL-test upgrading the pre-collaboration schema with existing refresh tokens.

### Authentication and tenancy contracts

- Evolve `AuthenticatedUser` without breaking frontend field access: retain `Workspace` as the **active** workspace; add `Workspaces` containing membership role data. `AuthenticationResponse` remains access token + expiry + user. `GET /api/auth/me` returns this same enriched user projection.
- `JwtAccessTokenIssuer.Issue` must include a private workspace-id claim. `CurrentWorkspaceResolver` reads both `NameIdentifier` and that claim, parses GUIDs, and SQL checks a matching membership with *any defined role*. It returns `null` for absent/malformed claim, missing membership, or no workspace. This makes old tokens unusable after deployment unless refresh obtains a new one, which is acceptable; it fails closed.
- Login retains the personal/Owner workspace as initial active workspace (only Owner is unique in the intended schema). Refresh looks up the token under current serializable flow, verifies its `ActiveWorkspaceId` still has a membership for the token user, and rotates into a token with that same id. `GetUserAsync` should accept/derive active workspace and validate it rather than selecting the first membership.
- `SwitchWorkspaceAsync` receives the authenticated user id, current refresh cookie, and requested workspace id. Under serializable execution, verify active refresh token and user ownership, verify requested membership, update/persist its `ActiveWorkspaceId`, issue an access token for that workspace, and return the full session. Do not let a bearer token select a workspace without validating the refresh-cookie session; do not put workspace selection in localStorage or a spoofable request header.
- Invite endpoints must derive both actor and workspace from the protected request resolver. Owner authorization is a dedicated membership lookup/policy/service check for the active workspace, not an ASP.NET Identity global role. Recipient endpoints derive recipient identity from the JWT subject and compare normalized server-side email from Identity.

Suggested minimal API surface:

- `GET /api/auth/me` — enriched active workspace and memberships.
- `POST /api/auth/workspaces/{workspaceId}/switch` — authenticated; current refresh cookie required; returns `AuthenticationResponse` and refresh cookie.
- `GET /api/workspace/invitations` / `POST /api/workspace/invitations` / `DELETE /api/workspace/invitations/{id}` — active Owner only (list/create/revoke).
- `GET /api/invitations/mine` / `POST /api/invitations/{id}/accept` — authenticated email-bound recipient only.

A namespaced `WorkspaceController` is consistent with existing controllers; use MediatR commands/query records, validators, application port, EF implementation, DI registration, and global problem mapping matching catalog slices.

### Frontend

- Add membership role/type data and a `switchWorkspace(workspaceId)` auth API call. On switch success call `useAuthStore.setSession(response)`; it already clears QueryClient if active workspace changes. Do not alter token manually.
- Replace Topbar's display-only workspace text (`frontend/src/components/layout/Topbar.tsx:104-109`) with an accessible selector from `user.workspaces`, showing active workspace and invoking switch. Disable/error gracefully if the switch fails; do not optimistically replace active workspace.
- Add a focused protected `/users` (existing placeholder at `frontend/src/app/router/index.tsx:67`) invitation page only if owner administration UI is required in this slice. It needs owner gating from session membership. Recipient accept may be exposed there or via a small invitations route. Avoid a broad profile/settings feature.
- Existing session bootstrap and Axios response interceptor must consume enriched refresh/switch responses. Preserve its single refresh promise and `sessionEpoch`; do not create a competing refresh flow during switch.

## Required tests and acceptance checks

### Domain/unit
- `WorkspaceMember` accepts Owner/Member and rejects undefined role; invitation normalizes email, validates UTC/expiry/state transitions, and cannot accept/revoke invalidly.
- `RefreshToken` validates non-empty active workspace and preserves invariants if its constructor/model changes.

### SQL Server Testcontainers integration (mandatory)
- Migration from the prior migration with legacy users **and refresh tokens** backfills `ActiveWorkspaceId`, preserves login/refresh, and applies successfully.
- Owner can create/list/revoke a pending Member invitation. A Member and unrelated user get 403/404 as appropriate and cannot enumerate or mutate the owner's invitation.
- Recipient acceptance requires authenticated normalized-email equality; wrong account cannot accept. Expired/revoked invitation cannot accept. Repeated/concurrent accept produces exactly one `WorkspaceMembers` row and one consumed invitation state.
- Pending duplicate invite/reinvite race cannot create two pending rows; status/replacement behavior is deterministic.
- After accept, the Member can switch into the invited workspace and see its operational data; before switch their personal workspace remains active. A different user's workspace id cannot switch. Missing/invalid refresh cookie cannot switch.
- Refresh after switch returns the selected active workspace; JWT resolver returns only its claimed workspace when that membership exists. A valid token whose membership is removed directly in SQL is denied by an existing protected operational endpoint, proving per-request SQL revalidation rather than claim-only authorization.
- Existing two-user product/inventory isolation tests continue to pass with an Owner active workspace; add a Member active-workspace operational test to document the deliberately broad P0 operational permission.
- Preserve refresh replay and logout-vs-refresh concurrency tests in `AuthenticationEndpointsTests.cs:94-177`; add switch-vs-refresh serialization coverage if updating the same refresh row.

### Validation commands

```bash
dotnet build backend/InventoryFlow.sln
dotnet test backend/InventoryFlow.sln
dotnet format backend/InventoryFlow.sln --verify-no-changes --no-restore
cd frontend && bun run typecheck && bun run lint && bun run build && bunx prettier --check .
```

Docker must be available for the integration suite; if unavailable, report that SQL tests were not run rather than replacing them with InMemory coverage.

## Review findings / risks

1. **High — current resolver prevents collaboration:** `CurrentWorkspaceResolver.cs:18-23` filters to Owner and requires exactly one match. Adding invitation acceptance alone yields authenticated Members who receive 403 on every operational API. Replace it atomically with signed-active-workspace plus SQL membership validation.
2. **High — refresh cannot retain a workspace selection:** `RefreshToken.cs:50-57` has no active workspace; `IdentityAuthenticationService.cs:73-79` reselects a sole Owner on refresh. A client-only switch would silently revert on refresh and risks stale cache/data context. Persist selection to the refresh session and rotate it.
3. **High — role expansion without authorization policy is an escalation risk:** all operational controllers authorize only authentication + resolved workspace. Explicitly define P0 Member operational access and Owner-only membership admin; do not claim finer permissions until a permission matrix and tests exist.
4. **Medium — invitations cannot safely be treated as emailed bearer links without mail/token lifecycle infrastructure:** no email provider/queue exists (`README.md` future scope; no mail dependency). Keep invitations authenticated and email-bound, do not store/emit a reusable raw invitation secret in P0.
5. **Medium — JWT workspace claim alone becomes stale after revocation:** resolver must SQL re-check membership every protected request. A cache, if introduced later, needs positive/negative invalidation for accept/revoke/remove/switch and short TTL; do not add Redis now.
6. **Medium — active workspace FK migration is data-sensitive:** the existing `AddWorkspaces` backfill creates a workspace/member, but refresh tokens predate collaboration. Test migration from the previous schema with a token and define fail-closed behavior for malformed legacy membership rather than arbitrary selection.

## Meta-prompt contract for the implementation agent

**Goal:** Implement the single collaboration foundation described above: Owner/Member memberships, secure persisted invitations, signed-and-SQL-validated active workspace selection, refresh-stable switching, minimal UI, EF migration, and SQL-backed lifecycle/security tests. Do not implement broad RBAC or platform delivery.

**Evidence/constraints:** The current workspace resolver is Owner-only (`backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs:18-23`), all operational controllers depend on it, and frontend cache identity uses `user.workspace.id`. Preserve controller-derived tenancy, current refresh replay/logout guarantees, and cache clearing in `auth-store.setSession`. Workspace selection must never be a trusted request body/header/localStorage value; JWT claim plus membership SQL validation is the security invariant.

**Success criteria:** An Owner invites a registered email as Member; only that user can accept exactly once; both accounts retain isolated personal workspaces; the recipient can deliberately switch to the shared workspace, refresh without reverting, and operate there; unauthorized users/roles/expired/revoked invitations/malformed or stale sessions fail closed. Existing operation and token tests remain green.

**Suggested approach:** First change domain/persistence/migration and auth/session projection; then resolver and owner authorization; then invitation application/API infrastructure; then React selector/minimal invitations UI; finally add/update unit and SQL integration tests. Generate the migration rather than hand-maintaining snapshot only.

**Validation:** Run the commands above, especially the full Testcontainers suite. Inspect `git diff --check`. Report any unavailable Docker-dependent test explicitly.

**Stop/escalation:** Ask the supervisor before adding roles beyond Owner/Member, allowing Member-level feature permissions, unauthenticated/email-link acceptance, outbound mail, invitation signup, workspace creation/deletion, member removal, or Redis. Stop when all P0 acceptance cases pass; do not broaden to those deferred areas.

**Resolved assumptions:** P0 recipients are already registered and must authenticate with the exact invited normalized email; acceptance does not auto-switch; Members may use current operational APIs; one personal Owner workspace remains the login default. No email is sent.

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "Concrete architecture findings, file paths, line ranges, security severity, implementation boundary, and validation plan are documented above."
    }
  ],
  "changedFiles": [
    ".pi-subagents/artifacts/outputs/301d1bae-ef00-4468-94c8-7de575f082cc/parallel-0/1-context-builder/context.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "git status --short && git log --oneline -12 && git branch --show-current && git show --stat --oneline HEAD",
      "result": "passed",
      "summary": "Confirmed operational workflow commits through reports and no tracked working-tree edits; only runtime artifacts are untracked."
    }
  ],
  "validationOutput": [
    "Context-only task: no code or test execution was performed.",
    "Required implementation validation is specified in this handoff, including SQL Server Testcontainers coverage."
  ],
  "residualRisks": [
    "Role names/permissions beyond Owner and Member require product approval.",
    "No outbound email or unauthenticated invite acceptance is available in the recommended first slice.",
    "Active-workspace refresh-token migration must be tested against legacy refresh-token data."
  ],
  "noStagedFiles": true,
  "diffSummary": "No repository code edits; wrote the required context/meta-prompt artifact only.",
  "reviewFindings": [
    "high: backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs:18-23 - Owner-only, singleton resolution blocks collaborators and workspace switching.",
    "high: backend/src/InventoryFlow.Domain/Entities/RefreshToken.cs:50-57 - no active-workspace session field, so selection cannot survive refresh safely.",
    "high: backend/src/InventoryFlow.Api/Controllers/ProductsController.cs:10-49 and peer controllers - no role authorization beyond authentication/current workspace; scope must explicitly limit Member permissions."
  ],
  "manualNotes": "The recommended P0 intentionally uses authenticated email-bound invitations without a raw invite token or mail delivery, minimizing bearer-token and account-creation attack surface."
}
```