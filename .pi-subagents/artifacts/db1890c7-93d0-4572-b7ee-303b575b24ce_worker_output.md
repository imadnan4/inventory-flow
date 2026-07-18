Implemented workspace-tenancy foundation.

Changed files: domain workspace/membership models, EF mappings/migration, tenancy resolver, auth session contract/provisioning, API docs, SPA workspace display, and domain/integration tests.

Validation:
- `dotnet build backend/InventoryFlow.sln --no-restore` — passed
- `dotnet test backend/InventoryFlow.sln --no-build --no-restore` — passed (21 tests)
- `dotnet format backend/InventoryFlow.sln --verify-no-changes --no-restore` — passed
- Frontend typecheck, lint, build, and Prettier — passed
- No staged files; no commit created.

Open risks/questions: legacy-user backfill SQL is included and exercised during SQL Server migration, but an isolated test that explicitly seeds a pre-workspace legacy user remains desirable.

Recommended next step: independent tenancy/security review before committing.