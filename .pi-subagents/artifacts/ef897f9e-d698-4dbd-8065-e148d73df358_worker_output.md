Implemented the P0 browser authentication slice.

Changed files: backend auth contracts/service/JWT/cookie endpoints/migration; frontend session store, guards, forms, API recovery, Topbar; configuration and docs.

Validation: backend build/tests/format passed; frontend typecheck/lint/build/Prettier passed; no staged files.

Open risks/questions: SQL Server Testcontainers authentication/replay integration tests were not added; existing integration tests only cover health/error hosting. `.pi-subagents/` remains untracked artifact output.

Recommended next step: run an independent security/code review, add relational auth/replay tests, then commit/push.