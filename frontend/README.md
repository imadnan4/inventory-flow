# Inventory Flow Web

The React single-page application for Inventory Flow.

## Stack

React 19, TypeScript, Vite, shadcn/ui, Tailwind CSS v4, React Router v7, TanStack Query/Table, React Hook Form, Zod, Axios, Zustand, Hugeicons, Recharts, Sonner, and Motion.

## Commands

```bash
bun install
bun run dev
bun run typecheck
bun run lint
bun run build
```

## Configuration

Copy `.env.example` to `.env` and set `VITE_API_BASE_URL` to the API origin. The default is `http://localhost:5000`.

## Structure

- `src/app`: application configuration, providers, and routing
- `src/components`: generated shadcn/ui primitives and shared layout components
- `src/features`: feature-owned UI, API clients, schemas, types, and hooks
- `src/layouts`: route layouts
- `src/lib`: shared client infrastructure
- `src/store`: Zustand client-only state
