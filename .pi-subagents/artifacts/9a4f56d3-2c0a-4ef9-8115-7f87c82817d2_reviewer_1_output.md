## Review
**APPROVED** — no Critical/High contract, cache, idempotency, or auth issues found.

- Correct: `frontend/src/features/purchases/purchases-api.ts:7-17` matches the backend GET/POST route and sends the idempotency key in both request body and `Idempotency-Key` header.
- Correct: `frontend/src/features/purchases/pages/PurchasesPage.tsx:76-95,192-200` retains failed mutation variables and retries with the same idempotency key.
- Correct: `frontend/src/features/purchases/pages/PurchasesPage.tsx:42-74` scopes queries by user/workspace and invalidates receipt history plus all matching inventory-balance cache variants after success.
- Correct: `frontend/src/app/router/index.tsx:31-77` nests `/purchases` under `RequireAuth`.
- Note: No frontend test files exist, including purchase-specific tests; typecheck, lint, and production build pass.
- Note: Requested root `plan.md` and `progress.md` were not present.