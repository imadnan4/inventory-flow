Implemented delivery baseline: CI, Docker/Compose stack, migration runner, same-origin SPA proxy/fallback, env example, and runbook.

Changed files: `.github/workflows/ci.yml`, Docker/Compose files, frontend proxy config, docs/env examples.

Validation: backend restore/build/test/format, frontend frozen install/typecheck/lint/build, Compose config/build/full startup, API health, SPA proxy.

Open risks/questions: CI integration tests require Docker/Testcontainers; Compose is intentionally local HTTP only.

Recommended next step: independent reviewer gate.