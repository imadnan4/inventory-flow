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

## Testing

### Test Projects

| Project | Scope | Type |
|---|---|---|
| `InventoryFlow.UnitTests` | Domain entities, value objects | Pure unit (no mocks) |
| `InventoryFlow.Application.Tests` | Application handlers, validators, DTOs | Unit with mocks (NSubstitute) |
| `InventoryFlow.IntegrationTests` | API endpoints, repositories | Integration (Testcontainers/SQL Server) |
| `InventoryFlow.ArchTests` | Layer dependency rules | Architecture (NetArchTest) |
| `frontend` (Vitest + RTL) | Components, hooks, utilities | Unit + component tests |

### Running Tests

```bash
# Backend - all tests
dotnet test backend/InventoryFlow.sln

# Backend - unit tests only
dotnet test backend/InventoryFlow.sln --filter "FullyQualifiedName~UnitTests"

# Backend - integration tests only (requires Docker + SQL Server)
dotnet test backend/InventoryFlow.sln --filter "FullyQualifiedName~IntegrationTests"

# Backend - architecture tests (no infrastructure)
dotnet test backend/InventoryFlow.sln --filter "FullyQualifiedName~ArchTests"

# Frontend - watch mode
cd frontend && bun run test

# Frontend - CI run
cd frontend && bun run test:run

# Frontend - coverage
cd frontend && bun run test:coverage

# Frontend - E2E
cd frontend && bun run e2e
```

### Frontend Testing Strategy

We use the **Testing Trophy** approach:
- **Vitest** for unit and component tests (fast feedback, runs on every commit)
- **React Testing Library** for user-facing component tests (test behavior, not implementation)
- **MSW** for API mocking in tests (intercepts network layer, not fetch calls)
- **Playwright** for critical E2E paths (auth flows, core feature journeys)

### Test Naming Convention

```csharp
// Format: ClassName_MethodName_Scenario_ExpectedBehavior
[Fact]
public void Handle_WhenProductNotFound_ReturnsNotFound()

[Fact]
public void GetInventoryItems_WithEmptyDatabase_ReturnsEmptyList()
```

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