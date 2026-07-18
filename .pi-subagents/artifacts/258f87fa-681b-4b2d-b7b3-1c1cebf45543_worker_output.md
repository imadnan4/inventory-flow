Implemented bounded atomic warehouse transfers with serializable canonical balance locking, durable replay, migration, API, UI, and integration coverage.

Changed files: backend transfer domain/application/infrastructure/controller/configuration/migration/tests; frontend transfers feature/route/sidebar.

Validation: build, targeted/full backend tests, formatting, frontend typecheck/lint/build all passed.

Open risks/questions: direct `dotnet ef database update` smoke was not run against a separately configured local SQL Server; real SQL Server integration tests exercised the migration path.

Recommended next step: reviewer acceptance.