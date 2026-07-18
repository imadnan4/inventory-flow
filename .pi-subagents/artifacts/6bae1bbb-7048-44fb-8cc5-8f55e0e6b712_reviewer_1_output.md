## Review
- Correct: **APPROVED** — no Critical/High contract, cache, idempotency, or auth issues found in the Sales UI.
  - `frontend/src/features/sales/sales-api.ts:16-18` sends the idempotency key in the required header.
  - `frontend/src/features/sales/pages/SalesPage.tsx:87,164-168` retains the original payload/key for manual retry.
  - `frontend/src/features/sales/pages/SalesPage.tsx:111-116` invalidates fulfillment history and all matching inventory-balance query variants.
  - `frontend/src/features/sales/pages/SalesPage.tsx:89-103` scopes queries by authenticated user and workspace; `RequireAuth` protects the route.
  - Frontend typecheck, lint, and production build pass.
- Note: `/home/adnan/Projects/Inventory-Flow/plan.md` and `progress.md` were absent; reviewed the available Sales handoff context instead.