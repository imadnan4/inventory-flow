# Contribution Guide

## Branch Strategy

This project uses a two-branch workflow:

| Branch | Purpose |
|--------|---------|
| `main` | Production-ready code. Always deployable. |
| `develop` | Integration branch. Synced with `main` after merges. |

### Workflow for every feature

1. **Create a feature branch** from `develop`:
   ```bash
   git checkout develop
   git pull origin develop
   git checkout -b feature/<short-descriptive-name>
   ```
   Use one of these prefixes:
   - `feature/` — new functionality (e.g., `feature/inventory/ledger`)
   - `fix/` — bug fix (e.g., `fix/auth-null-check`)
   - `chore/` — maintenance tasks (e.g., `chore/update-deps`)

2. **Commit changes** with clear, conventional commit messages:
   ```
   feat: add inventory movement ledger
   fix: null check on idempotency key
   chore: update docker-compose build context
   ```

3. **Push the branch** and open a **Pull Request** against `develop` (not `main`).
   - PRs must pass CI (backend tests + frontend build).
   - PRs tagged `coderabbit` will receive automated review comments.
   - Address all CodeRabbit actionable comments before merging.

4. **After PR approval**, merge into `develop`.

5. **Sync `main` with `develop`** when ready for release:
   ```bash
   git checkout main
   git merge develop
   git push origin main
   ```

## CI/CD

- CI runs automatically on PRs and pushes to `main`.
- Backend: Restore → Build → Test → Format check (all .NET 9).
  - Integration tests use **Testcontainers** to spin up a SQL Server Docker container automatically in CI.
  - For local testing, ensure Docker is running (`docker-compose up -d sql`).
- Frontend: Install → Typecheck → Lint → Build (Bun + Vite).

## Code Review

- CodeRabbit runs automated review on every PR.
- Review profile: **CHILL** (minimal, high-signal comments only).
- Resolve all **actionable** (🟠/🔴) comments before merging.

## Project Structure

```
backend/           .NET 9 solution (C#)
  src/             Application, Domain, Infrastructure, API projects
  tests/           Unit tests + Integration tests
frontend/          React + TypeScript (Vite + Bun)
  src/             App components, features, lib
docker-compose.yml  Local dev infrastructure (SQL Server, API, Web)
```