## Review

- **Correct:** Routing is centralized in `frontend/src/app/router/index.tsx:23-46`, and the dashboard shell is isolated in `frontend/src/layouts/DashboardLayout.tsx:8-22`. The shell has responsive navigation and accessible labels (`frontend/src/components/layout/Sidebar.tsx:47`, `Topbar.tsx:46-47`).
- **Correct:** TanStack Query, toast, theme, and tooltip providers are already composed at the app root (`frontend/src/app/providers/AppProviders.tsx:13-21`), providing a suitable location for a session provider/gate.
- **Blocker:** API connectivity is not runnable as configured. The SPA default/example targets port `5000` (`frontend/src/app/config/environment.ts:1`, `frontend/.env.example:1`), while the API launch profile uses `5255` (`backend/src/InventoryFlow.Api/Properties/launchSettings.json:8`). Further, the CORS allow-list is empty (`backend/src/InventoryFlow.Api/appsettings.json:3-5`), so browser API calls from Vite will not receive an allowed origin.
- **Blocker:** The backend has Identity persistence but no authentication implementation: `Program.cs` registers authorization only (`backend/src/InventoryFlow.Api/Program.cs:18,49`); there is no `AddAuthentication`, JWT bearer scheme, `UseAuthentication`, auth controller, or auth endpoints. An auth frontend cannot integrate yet.
- **High:** Every application route is public under `DashboardLayout` (`frontend/src/app/router/index.tsx:24-44`); anonymous users can directly open `/dashboard` and all placeholder routes. A protected route boundary must wrap this layout before it mounts.
- **High:** `apiClient` has neither credential support nor bearer-token/401-refresh handling (`frontend/src/lib/api-client.ts:5-10`). It cannot support an HttpOnly refresh cookie or recover an expired access token.
- **Medium:** The header presents a hard-coded user and inert sign-out action (`frontend/src/components/layout/Topbar.tsx:89-100`). This would misrepresent session state after login/logout.
- **High:** No frontend test runner, test script, or frontend tests exist (`frontend/package.json:6-13`). Authentication guards and refresh behavior would be unprotected from regressions.
- **Note:** `queryClient` retries queries once globally (`frontend/src/lib/query-client.ts:3-10`). The session-refresh query must explicitly use `retry: false`; retrying a request after an ambiguous refresh-token rotation can create confusing logout behavior.
- **Note:** The dashboard is static fixture data (`frontend/src/features/dashboard/pages/DashboardPage.tsx:40-96`), acceptable as a shell but it must not be described as real-time data until authenticated API data is wired.

### Smallest professional authentication slice

1. **UX**
   - Add a public `/login` page outside `DashboardLayout`: centered branded card, labelled email/password fields, `autocomplete="email"` and `current-password`, show/hide-password control, inline generic invalid-credentials message, disabled/loading submit button, and focus on the first invalid field.
   - Omit registration, password recovery, and “remember me” until server contracts exist.
   - On startup, show a short accessible “Restoring session” gate—never the login form or dashboard briefly—while refresh is pending.
   - Bind the topbar avatar/name to the authenticated user; make Sign out revoke the session, clear client cache, and navigate to `/login`.

2. **Routing/session state**
   - Add an auth feature with a memory-only access token and session/user state; do not place access or refresh tokens in localStorage/Zustand persistence.
   - Add `RequireSession` around the existing dashboard route tree in `frontend/src/app/router/index.tsx:24-44`. Anonymous access redirects with `replace` to `/login`, retaining only a validated same-origin `from` path.
   - Authenticated visits to `/login` redirect to the retained destination or `/dashboard`. Logout clears all user-scoped TanStack Query cache before redirecting.

3. **Minimum API contract**
   - `POST /api/auth/login` accepts `{ email, password }`; returns `{ accessToken, expiresAtUtc, user: { id, email, displayName, roles } }` and sets a rotated refresh cookie.
   - `POST /api/auth/refresh` has no body; reads/rotates the HttpOnly refresh cookie and returns the same session payload. Invalid/expired/reused token returns generic `401`.
   - `POST /api/auth/logout` revokes the presented refresh token, clears its cookie, and returns `204`; it should be safe to repeat.
   - Use `application/problem+json` for validation/server errors, matching existing error handling in `backend/src/InventoryFlow.Api/ExceptionHandling/GlobalExceptionHandler.cs:31-58`. Invalid login responses must not reveal whether an email exists.
   - Configure JWT validation and middleware before authorization, and apply `[Authorize]` by default to protected application endpoints. The existing hashed, unique refresh-token persistence is a usable foundation (`backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs:16-30`).

4. **Client/API integration**
   - Set the development API origin consistently and allow that exact frontend origin in CORS. For the cookie strategy, Axios must use `withCredentials: true`; CORS must use explicit origins plus `AllowCredentials()`.
   - Attach the memory access token to protected requests. Implement a single-flight refresh interceptor: exclude login/refresh/logout, refresh once for concurrent `401`s, replay once after success, and clear/navigate on failure without loops.

5. **Required tests**
   - Add Vitest, React Testing Library, and MSW.
   - Frontend: anonymous protected-route redirect/no shell flash; authenticated login redirect; saved destination restoration; validation/error/loading accessibility; logout cache clear; one concurrent refresh for multiple `401`s; failed refresh does not retry or loop.
   - Backend integration: login success/failure/lockout, refresh rotation and replay rejection, logout revocation, JWT-protected endpoint, cookie attributes, CORS preflight/credential response, and generic `401` behavior.