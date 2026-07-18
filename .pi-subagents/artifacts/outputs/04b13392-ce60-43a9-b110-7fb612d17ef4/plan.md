# Implementation Plan

## Goal
Establish a durable workspace-tenancy foundation in which every new self-registered user owns one personal workspace, every issued session exposes that workspace, and future workspace-owned aggregates can resolve it server-side.

## Tasks
1. **Define the workspace domain model and invariants**
   - File: `backend/src/InventoryFlow.Domain/Entities/Workspace.cs` (new)
   - File: `backend/src/InventoryFlow.Domain/Entities/WorkspaceMember.cs` (new)
   - File: `backend/src/InventoryFlow.Domain/Entities/WorkspaceMemberRole.cs` (new, if a named enum is preferred over a string)
   - Changes: Add GUID-keyed `Workspace` with a required, normalized/trimmed display name and UTC creation timestamp. Add `WorkspaceMember` containing `WorkspaceId`, `UserId`, `Role`, and UTC creation timestamp. Reject empty IDs, blank/oversized names, and non-UTC timestamps with `DomainException`. Initially allow only the immutable `Owner` member role; do not add invitation, permission, or role-management behavior.
   - Acceptance: Unit tests prove valid construction and all domain invariant failures.

2. **Persist workspace and membership separately from Identity**
   - File: `backend/src/InventoryFlow.Infrastructure/Persistence/ApplicationDbContext.cs`
   - File: `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/WorkspaceConfiguration.cs` (new)
   - File: `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/WorkspaceMemberConfiguration.cs` (new)
   - Changes: Add `DbSet<Workspace>` and `DbSet<WorkspaceMember>`. Map `Workspaces` and `WorkspaceMembers` with required bounded columns, a unique `(WorkspaceId, UserId)` membership constraint, useful `UserId` and `WorkspaceId` indexes, and cascading workspace deletion. Keep the member-to-user foreign key mapped to `ApplicationUser` without placing Identity types in the Domain project.
   - Acceptance: EF model snapshot exposes both tables, constraints, indexes, and FK relationships.

3. **Add a migration that is safe for existing Identity users**
   - File: `backend/src/InventoryFlow.Infrastructure/Persistence/Migrations/<timestamp>_AddWorkspaces.cs` (new)
   - File: `backend/src/InventoryFlow.Infrastructure/Persistence/Migrations/<timestamp>_AddWorkspaces.Designer.cs` (new)
   - File: `backend/src/InventoryFlow.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs`
   - Changes: Generate the schema migration from the model. Include a set-based SQL Server data migration that creates exactly one personal workspace and Owner membership for every pre-existing `AspNetUsers` row that has no workspace membership. Use a migration-local mapping/table variable (or an equivalent safe SQL pattern) so each generated workspace GUID is paired with the originating user; do not join by display name or email. Name backfilled workspaces deterministically and within the configured maximum length.
   - Acceptance: Applying the migration to a database containing an existing Identity user preserves that user and produces one linked workspace/member record; a new database migrates cleanly.

4. **Create an application-level current-workspace contract**
   - File: `backend/src/InventoryFlow.Application/Common/Tenancy/CurrentWorkspace.cs` (new)
   - File: `backend/src/InventoryFlow.Application/Common/Tenancy/ICurrentWorkspace.cs` (new)
   - Changes: Define a small application-owned value/contract that represents the authenticated current workspace and a scoped resolver suitable for later aggregate handlers. The contract must be explicit about unauthenticated and missing-membership behavior (return nullable context or throw a dedicated application-safe exception); do not introduce generalized authorization policies.
   - Acceptance: No Application project type references Infrastructure or ASP.NET Identity directly; later product commands can depend only on this contract.

5. **Implement the resolver and register request prerequisites**
   - File: `backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs` (new)
   - File: `backend/src/InventoryFlow.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
   - Changes: Resolve the authenticated user ID from the request claims, query the member/workspace pair through `ApplicationDbContext`, and return the user’s sole Owner workspace. Register `IHttpContextAccessor` and the scoped resolver. Treat missing/invalid claims or missing membership as no current workspace rather than silently selecting arbitrary data. Although the schema supports future multi-membership, fail safely if the invariant expected by this slice is violated instead of introducing an implicit workspace-selection rule.
   - Acceptance: Resolver queries are scoped by the authenticated user and cannot return another user’s workspace.

6. **Provision the workspace atomically during registration**
   - File: `backend/src/InventoryFlow.Infrastructure/Authentication/IdentityAuthenticationService.cs`
   - File: `backend/src/InventoryFlow.Application/Features/Authentication/IAuthenticationService.cs`
   - File: `backend/src/InventoryFlow.Application/Features/Authentication/AuthenticationModels.cs`
   - File: `backend/src/InventoryFlow.Application/Features/Authentication/AuthenticationHandlers.cs` (only if response/handler types require adjustment)
   - Changes: Wrap `UserManager.CreateAsync`, personal workspace construction, Owner membership insertion, and refresh-session issuance in the same SQL Server execution-strategy-aware transaction. Construct all retry-sensitive entities inside the execution-strategy delegate. On Identity creation failure, roll back without leaving a workspace/membership/token. Login, refresh, and `/me` must resolve and return the member’s workspace; a legacy/malformed account with no workspace must not receive a usable session. Extend the existing authentication response with a `workspace` object (`id`, `name`) under the existing `user` object or as a named sibling, consistently for register/login/refresh/me. Prefer a separate `AuthenticatedWorkspace` record to keep the HTTP contract clear.
   - Acceptance: A successful registration has exactly one user, workspace, Owner membership, and refresh-token family; a failed duplicate/invalid registration persists none of the new tenancy records.

7. **Expose the revised auth contract without changing refresh-cookie behavior**
   - File: `backend/src/InventoryFlow.Api/Controllers/AuthController.cs`
   - File: `backend/src/InventoryFlow.Api/InventoryFlow.Api.http`
   - File: `backend/README.md`
   - Changes: Preserve endpoint paths, bearer authentication, HTTP-only refresh cookie handling, and status semantics. Update XML/OpenAPI response annotations and HTTP samples/documentation to show the workspace response shape and explain that `Jwt__SigningKey` remains an environment secret. Do not put a workspace ID in the refresh cookie; the server remains the authority for workspace resolution.
   - Acceptance: Register, login, refresh, and `/me` return the same workspace identity for a session; logout behavior is unchanged.

8. **Update client session types and minimally display the workspace**
   - File: `frontend/src/features/auth/types.ts`
   - File: `frontend/src/features/auth/auth-store.ts` (only if state shape needs explicit handling)
   - File: `frontend/src/features/auth/auth-api.ts` (only if endpoint response inference requires it)
   - File: `frontend/src/components/layout/Topbar.tsx`
   - Changes: Extend the session/user TypeScript contract to match the API workspace object. Keep the workspace in the existing memory-only session store; do not add localStorage persistence or switching. Display the current workspace name in the account menu/header with a safe fallback while the session is loading or absent.
   - Acceptance: Typecheck passes and the top bar displays the workspace returned by registration/login/refresh.

9. **Add domain and SQL-backed integration coverage**
   - File: `backend/tests/InventoryFlow.UnitTests/Domain/WorkspaceTests.cs` (new)
   - File: `backend/tests/InventoryFlow.UnitTests/Domain/WorkspaceMemberTests.cs` (new)
   - File: `backend/tests/InventoryFlow.IntegrationTests/Api/AuthenticationEndpointsTests.cs`
   - File: `backend/tests/InventoryFlow.IntegrationTests/Api/AuthenticatedApiFixture.cs` (only if migration/fixture helpers are needed)
   - File: `backend/tests/InventoryFlow.IntegrationTests/Api/WorkspaceMigrationTests.cs` (new if legacy-data migration needs isolation)
   - Changes: Test domain invariants, registration persistence/Owner role, consistent workspace data through `/me` and refresh, and transaction rollback on failed registration. Add migration-level coverage for a pre-existing Identity user if the current Testcontainers fixture can seed the pre-migration schema; otherwise document and run an equivalent focused SQL migration verification. Retain all existing refresh-replay and logout concurrency tests.
   - Acceptance: SQL Server-backed tests prove workspace provisioning, auth response continuity, and no orphaned tenancy rows.

10. **Run the full quality gate and request a tenancy/security review**
   - File: no production file changes required
   - Changes: Run `dotnet build backend/InventoryFlow.sln`, `dotnet test backend/InventoryFlow.sln`, `dotnet format backend/InventoryFlow.sln --verify-no-changes --no-restore`, `bun run typecheck`, `bun run lint`, `bun run build`, and `bunx prettier --check .`. Have a fresh reviewer inspect tenant-isolation, registration transactionality, migration backfill correctness, response-contract compatibility, and deferred-scope discipline before merge.
   - Acceptance: All commands pass; review has no unresolved blocker or important issue.

## Files to Modify
- `backend/src/InventoryFlow.Infrastructure/Persistence/ApplicationDbContext.cs` - expose workspace sets.
- `backend/src/InventoryFlow.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` - register the scoped workspace resolver/accessor.
- `backend/src/InventoryFlow.Infrastructure/Authentication/IdentityAuthenticationService.cs` - transactional provisioning and workspace-aware sessions.
- `backend/src/InventoryFlow.Application/Features/Authentication/AuthenticationModels.cs` - extend the session/user contract.
- `backend/src/InventoryFlow.Application/Features/Authentication/IAuthenticationService.cs` - align service return contract.
- `backend/src/InventoryFlow.Api/Controllers/AuthController.cs` - document/expose unchanged endpoint behavior with the revised response.
- `backend/src/InventoryFlow.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs` - workspace model snapshot.
- `backend/tests/InventoryFlow.IntegrationTests/Api/AuthenticationEndpointsTests.cs` - workspace and transactional auth assertions.
- `frontend/src/features/auth/types.ts` - API workspace shape.
- `frontend/src/components/layout/Topbar.tsx` - minimal workspace display.
- `backend/README.md` and `backend/src/InventoryFlow.Api/InventoryFlow.Api.http` - contract/configuration examples.

## New Files
- `backend/src/InventoryFlow.Domain/Entities/Workspace.cs` - workspace aggregate/root for future scoped records.
- `backend/src/InventoryFlow.Domain/Entities/WorkspaceMember.cs` - user-to-workspace membership invariant.
- `backend/src/InventoryFlow.Domain/Entities/WorkspaceMemberRole.cs` - Owner-only role value/enum.
- `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/WorkspaceConfiguration.cs` - EF mapping.
- `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/WorkspaceMemberConfiguration.cs` - EF mapping/indexes/FKs.
- `backend/src/InventoryFlow.Application/Common/Tenancy/CurrentWorkspace.cs` - application tenancy context value.
- `backend/src/InventoryFlow.Application/Common/Tenancy/ICurrentWorkspace.cs` - resolver abstraction.
- `backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs` - request/user-to-workspace implementation.
- `backend/src/InventoryFlow.Infrastructure/Persistence/Migrations/<timestamp>_AddWorkspaces.cs` - schema and legacy-user backfill.
- `backend/src/InventoryFlow.Infrastructure/Persistence/Migrations/<timestamp>_AddWorkspaces.Designer.cs` - generated migration metadata.
- `backend/tests/InventoryFlow.UnitTests/Domain/WorkspaceTests.cs` - workspace invariants.
- `backend/tests/InventoryFlow.UnitTests/Domain/WorkspaceMemberTests.cs` - membership invariants.

## Dependencies
- Tasks 1–2 precede the migration, resolver, provisioning, and tests.
- Task 3 depends on the completed EF model in Task 2.
- Tasks 4–5 establish the reusable server-side contract before Task 6 uses it for auth results.
- Task 6 must finish before Tasks 7–8 can finalize the API and frontend contract.
- Task 9 follows the schema and auth behavior; Task 10 follows every implementation task.

## Risks
- **Critical — existing data:** A migration that only adds empty tables makes existing Identity users unable to establish sessions. The migration must backfill a one-to-one personal workspace/member pair without unsafe joins or duplicate creation.
- **Critical — transaction boundaries:** `UserManager.CreateAsync` persists through the shared `ApplicationDbContext`; workspace/member and refresh-token writes must use the same execution-strategy-aware transaction, including retries, to avoid partial registration state.
- **High — future multi-workspace semantics:** The data model supports many memberships, but this slice intentionally has no user-driven current-workspace selector. Resolver behavior must reject missing/ambiguous membership rather than silently choosing an arbitrary workspace.
- **High — tenancy isolation:** The future resolver must use authenticated claims and membership queries only; do not accept a workspace ID from a client request in this foundation slice.
- **Medium — API compatibility:** Authentication response changes require coordinated C# records, endpoint annotations, frontend types, and integration tests. Keep endpoint paths and cookie behavior stable.
- **Medium — migration verification:** Testcontainers SQL integration may make a true pre-migration backfill test more involved; do not replace it with an InMemory-provider test because it would not validate SQL Server migration behavior.

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "Concrete, ordered findings identify the required Domain, Application, Infrastructure, API, frontend, migration, and SQL-backed test files; risks include exact tenant-isolation and legacy-data concerns."
    }
  ],
  "changedFiles": [],
  "testsAddedOrUpdated": [
    "backend/tests/InventoryFlow.UnitTests/Domain/WorkspaceTests.cs",
    "backend/tests/InventoryFlow.UnitTests/Domain/WorkspaceMemberTests.cs",
    "backend/tests/InventoryFlow.IntegrationTests/Api/AuthenticationEndpointsTests.cs",
    "backend/tests/InventoryFlow.IntegrationTests/Api/WorkspaceMigrationTests.cs"
  ],
  "commandsRun": [
    {
      "command": "Read relevant authentication, persistence, integration-test, frontend type, and Topbar sources",
      "result": "passed",
      "summary": "Confirmed the existing session contract, shared EF/Identity context, Testcontainers fixture, and client session display integration points."
    }
  ],
  "validationOutput": [
    "Planning-only task; implementation validation commands are specified in Task 10."
  ],
  "residualRisks": [
    "A precise SQL Server backfill implementation must be validated against a database with existing AspNetUsers before release.",
    "Multi-workspace selection is deliberately deferred; missing or ambiguous memberships must fail safely."
  ],
  "noStagedFiles": true,
  "diffSummary": "No project/source changes made; produced the requested implementation plan artifact only.",
  "reviewFindings": [
    "critical: legacy Identity users require a data backfill migration or would have no current workspace/session.",
    "critical: registration must share one execution-strategy-aware transaction across Identity, tenancy, and refresh-token writes.",
    "high: do not create an arbitrary current-workspace selection rule before a switcher/persistence design exists."
  ],
  "manualNotes": "The approved product direction is encoded: registration creates one personal Owner workspace; invitations, switching, role management, products, and a generalized policy framework remain out of scope."
}
```