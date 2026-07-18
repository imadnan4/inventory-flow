Inherited decisions:
- Operational workflows are now workspace-scoped and released through inventory availability reporting.
- Self-registration creates one personal workspace with an immutable Owner membership; invitations and switching are explicitly deferred (`backend/README.md`).
- Current tenancy must remain server-resolved, never client-supplied.

Diagnosis:
Choose **platform delivery first**. There are no tracked Docker, Compose, or GitHub Actions files, while the documented quality checks are entirely manual (`README.md`). CI can exercise the existing SQL Server Testcontainers integration suite because Docker is available.

Collaboration is not a small additive slice:
- **High:** `backend/src/InventoryFlow.Domain/Entities/WorkspaceMember.cs` rejects every role except Owner.
- **High:** `backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs` considers only Owner memberships and fails closed when there is more than one.
- **High:** `frontend/src/features/auth/types.ts` exposes one workspace, not a membership/workspace list or active-workspace contract.
- All operational controllers depend on `ICurrentWorkspace`; switching must be designed centrally rather than accepted as a client workspace ID.

Drift / contradiction check:
Starting “roles/invitations/switching” now would contradict the deliberate single-workspace tenancy contract and risk cross-workspace authorization errors. ASP.NET Identity roles registered in `backend/src/InventoryFlow.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` are not a workspace authorization model.

Recommendation:
Implement a **delivery baseline**, limited to:
1. GitHub Actions on PRs and `main`: backend restore/build/test/format; frontend frozen-lockfile install/typecheck/lint/build.
2. A developer-only Compose stack: SQL Server, one-shot EF migration runner, API, and web SPA.
3. Multi-stage API/web Dockerfiles, `.dockerignore`, root environment example, and concise runbook.
4. Compose readiness ordering: SQL healthy → migration succeeds → API/web.
5. Build the SPA with same-origin `/api` and configure the web server with SPA fallback plus `/api` proxying.

Do not add CD, registry publishing, production TLS, production secrets, automatic API-startup migrations, or collaboration behavior.

The migration runner is required because `backend/src/InventoryFlow.Api/Program.cs` does not apply migrations, and current documentation requires a manual `dotnet ef database update`. Keep it separate from the API process.

For the local HTTP Compose profile, explicitly use Development or provide HTTPS: `backend/src/InventoryFlow.Api/Controllers/AuthController.cs` sets refresh cookies `Secure` outside Development, so a naive production-environment HTTP stack will make login fail.

Risks:
- **High if missed:** HTTP Compose with Production environment rejects the secure refresh-cookie flow (`AuthController.cs`).
- **Medium:** Testcontainers integration tests require a Docker-capable CI runner (`backend/tests/InventoryFlow.IntegrationTests/Api/AuthenticatedApiFixture.cs`).
- **Medium:** Compose must obtain JWT and SQL credentials only from ignored environment input; never commit defaults.
- Collaboration still requires an explicit role/permission matrix, invitation-token lifecycle, active-workspace selection, authorization enforcement, and migration/backfill rules after platform delivery.

Need from main agent:
- None.

Suggested execution prompt:
- No implementation handoff is warranted for this recommendation-only task.