Implemented review fixes and SQL Server auth integration coverage.

Changed files: backend auth service/config/tests; frontend auth client, redirects, and forms.

Validation: full backend tests (13 passed), focused Testcontainers auth tests (5 passed), frontend typecheck/lint/Prettier/build, backend format check.

Open risks/questions: Browser-level auth/accessibility flows are not E2E-tested.

Recommended next step: Independent final review before commit.