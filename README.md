# Inventory Flow

A production-oriented inventory management SaaS built as a monorepo.

## Technology

- **Backend:** ASP.NET Core 9, Clean Architecture, EF Core, SQL Server, ASP.NET Identity, JWT, FluentValidation, MediatR, AutoMapper, Serilog, Hangfire, Redis, xUnit
- **Frontend:** React 19, TypeScript, Vite, shadcn/ui, Tailwind CSS v4, React Router, TanStack Query/Table, React Hook Form, Zod, Zustand
- **Operations:** Docker Compose, GitHub Actions, Azure-ready configuration

## Repository layout

```text
backend/    ASP.NET Core solution and tests
frontend/   React single-page application
```

## Branching model

- `main` contains production-ready releases.
- `develop` is the integration branch.
- Frontend work branches from `develop` as `feature/frontend/<feature-name>`.
- Backend work branches from `develop` as `feature/backend/<feature-name>`.
- Fixes use `bugfix/frontend/<description>` or `bugfix/backend/<description>`.

Changes are merged through pull requests into `develop`; releases are promoted from `develop` to `main`.

## Prerequisites

- .NET SDK 9.0.316 (pinned in `global.json` and `mise.toml`)
- Bun 1.3+
- Docker Desktop or Docker Engine (for local infrastructure)

## Status

Project foundation is being established. See pull requests and commits for milestone history.
