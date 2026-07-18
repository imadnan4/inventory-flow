# Inventory Flow

**Inventory Flow** is an enterprise-oriented inventory management SaaS for tracking products, stock, warehouses, suppliers, purchasing, sales, and operational activity from one workspace.

> [!NOTE]
> The project is under active development. The current milestone establishes the production foundations: Clean Architecture, Identity persistence, refresh-token lifecycle rules, a responsive React shell, and a dashboard experience.

## Highlights

- **Clean .NET backend** — ASP.NET Core 9, MediatR, FluentValidation, Serilog, centralized package management, RFC 7807 errors, rate limiting, health checks, and xUnit coverage.
- **Identity-ready persistence** — SQL Server, ASP.NET Core Identity, GUID keys, refresh-token storage, EF Core migrations, and retry-aware database configuration.
- **Enterprise React UI** — React 19, TypeScript, Vite, shadcn/ui, Hugeicons, Tailwind CSS v4, responsive navigation, light/dark themes, and a code-split dashboard.
- **Professional workflow** — public repository, `main`/`develop` branches, scoped feature branches, pull requests, reviews, and automated local validation commands.

## Architecture

```text
React SPA (frontend)
        │ HTTPS / REST
ASP.NET Core API (backend/src/InventoryFlow.Api)
        │
Application → Domain ← Infrastructure
        │                  │
     MediatR            EF Core / Identity
                           │
                       SQL Server
```

```text
backend/
  src/
    InventoryFlow.Api/             HTTP pipeline and API composition
    InventoryFlow.Application/     use cases, handlers, validators, mappings
    InventoryFlow.Domain/          entities, value objects, business rules
    InventoryFlow.Infrastructure/  EF Core, Identity, persistence integrations
    InventoryFlow.Shared/          cross-cutting contracts
  tests/
    InventoryFlow.UnitTests/
    InventoryFlow.IntegrationTests/

frontend/
  src/
    app/                           configuration, providers, routing
    components/                    shadcn primitives and shared UI
    features/                      feature-owned pages, hooks, schemas, APIs
    layouts/                       route layouts
    lib/                           shared client infrastructure
    store/                         Zustand client-only state
```

## Technology

| Area | Tools |
| --- | --- |
| Backend | ASP.NET Core 9, C#, EF Core, SQL Server, ASP.NET Identity, MediatR, FluentValidation, AutoMapper, Serilog |
| Frontend | React 19, TypeScript, Vite, React Router v7, TanStack Query/Table, React Hook Form, Zod, Axios, Zustand |
| Design | shadcn/ui, Tailwind CSS v4, Hugeicons, Recharts, Sonner, Motion |
| Tooling | Bun, xUnit, EF Core local tool manifest |

## Get started

### Prerequisites

- Docker with Docker Compose
- Or, for running services directly: [.NET SDK 9.0.316](https://dotnet.microsoft.com/download/dotnet/9.0), [Bun 1.3+](https://bun.sh/), and SQL Server 2022+

### Docker Compose (recommended)

```bash
cp .env.example .env
# Edit .env: provide a strong SQL Server SA password and a random JWT signing key (at least 32 bytes).
docker compose up --build
```

Compose is a local-development stack only. SQL Server must become healthy before its one-shot `migrate` service applies EF migrations; the API and web services then start. Open `http://localhost:8080`; the SPA proxies `/api` to the API and uses SPA fallback routing. The API runs in `Development` so refresh cookies remain valid over local HTTP. Stop it with `docker compose down` (add `-v` to remove local database data).

No credentials or JWT signing keys are committed. Do not use this HTTP Compose configuration for production.

### Run services directly

Set configuration outside tracked files, then apply migrations:

```bash
export ConnectionStrings__InventoryFlowDatabase='Server=localhost,1433;Database=InventoryFlow;User Id=sa;Password=<strong-password>;TrustServerCertificate=True'
export Jwt__SigningKey='<at-least-32-random-bytes>'
cd backend
dotnet tool restore
dotnet restore
dotnet ef database update --project src/InventoryFlow.Infrastructure --startup-project src/InventoryFlow.Api
```

In separate terminals, run `dotnet run --project backend/src/InventoryFlow.Api` and `cd frontend && bun install --frozen-lockfile && bun run dev`. The Vite server proxies `/api` to `http://localhost:5255`; its frontend is at `http://localhost:5173`.

## Quality checks

```bash
# Backend
dotnet build backend/InventoryFlow.sln
dotnet test backend/InventoryFlow.sln
dotnet format backend/InventoryFlow.sln --verify-no-changes --no-restore

# Frontend
cd frontend
bun run typecheck
bun run lint
bun run build
bunx prettier --check .
```

## Delivery workflow

- `main` contains production-ready releases.
- `develop` is the integration branch.
- Frontend work uses `feature/frontend/<feature-name>`.
- Backend work uses `feature/backend/<feature-name>`.
- Fixes use `bugfix/frontend/<description>` or `bugfix/backend/<description>`.

Every change is committed on a scoped branch, validated locally, reviewed in a pull request into `develop`, and promoted to `main` for release. GitHub Actions validates pull requests and `main` with backend restore/build/test/format and frontend frozen Bun install/typecheck/lint/build.

## Current scope

Implemented foundations include the API pipeline, global problem-details handling, health checks, SQL Server Identity schema, refresh-token domain model and migration, and the responsive dashboard shell.

Upcoming vertical slices cover role and permission enforcement, Redis caching, and Hangfire jobs. This repository intentionally contains no CD, registry publishing, production TLS, production secrets, or API startup migrations.
