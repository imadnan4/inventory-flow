# Implementation Plan

## Goal
Deliver the P0 browser authentication vertical slice: Identity-backed registration/login, short-lived JWT access tokens, secure HttpOnly refresh-cookie rotation with replay-family revocation, logout/me, and a guarded SPA session.

## Tasks
1. **Define the authentication application contract and request validation.**
   - Files: `backend/src/InventoryFlow.Application/Features/Authentication/AuthenticationModels.cs`, `backend/src/InventoryFlow.Application/Features/Authentication/IAuthenticationService.cs`, `backend/src/InventoryFlow.Application/Features/Authentication/RegisterUser.cs`, `LoginUser.cs`, `RefreshSession.cs`, `LogoutSession.cs`, `GetCurrentUser.cs`, and corresponding `*Validator.cs` files; `backend/src/InventoryFlow.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`; `backend/src/InventoryFlow.Api/ExceptionHandling/GlobalExceptionHandler.cs`
   - Changes: Add MediatR commands/queries and public response models for `{ accessToken, accessTokenExpiresAtUtc, user }`; keep the raw refresh value only in the application-to-API session result so it can be set as a cookie and never serialized. Define an `IAuthenticationService` application port implemented by Infrastructure. Validate display name (required, trimmed, max 200), email, and non-empty password/request fields with FluentValidation; leave the full password-policy decision to Identity. Register a MediatR validation pipeline behavior and map `ValidationException` to RFC 7807 400 validation details. Authentication failures must be represented/mapped as one generic 401 response, never user-existence or lockout details.
   - Acceptance: API request validation returns 400 Problem Details; invalid and unknown login credentials produce the same 401 contract; Application has no Infrastructure reference.

2. **Extend persisted refresh tokens with a replay-revocation family and migrate safely.**
   - Files: `backend/src/InventoryFlow.Domain/Entities/RefreshToken.cs`, `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs`, `backend/src/InventoryFlow.Infrastructure/Persistence/Migrations/<timestamp>_AddRefreshTokenFamilies.cs`, `backend/src/InventoryFlow.Infrastructure/Persistence/Migrations/<timestamp>_AddRefreshTokenFamilies.Designer.cs`, `backend/src/InventoryFlow.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs`
   - Changes: Add required immutable `FamilyId` (`Guid`) to `RefreshToken`, validate it is non-empty, and index it for family-wide revocation. Generate an EF migration that adds/backfills the non-null field (each pre-existing token gets its own generated family), then adds the family index; do not edit the already-applied initial migration. Keep the existing unique SHA-256 hash index and never add a plaintext token column.
   - Acceptance: `dotnet ef migrations add` produces a checked-in migration/model snapshot; applying it preserves existing refresh rows and gives each one a usable family ID.

3. **Implement Infrastructure identity, JWT, opaque refresh issuance, and atomic rotation.**
   - Files: `backend/src/InventoryFlow.Infrastructure/Authentication/JwtOptions.cs`, `JwtAccessTokenIssuer.cs`, `RefreshTokenGenerator.cs`, `IdentityAuthenticationService.cs`, `backend/src/InventoryFlow.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`, `backend/Directory.Packages.props`, `backend/src/InventoryFlow.Infrastructure/InventoryFlow.Infrastructure.csproj`
   - Changes: Add the JWT bearer package/version and bind/validate options at startup: required issuer, audience, and an environment/secret-only HMAC signing key of at least 32 random bytes; short access lifetime (10 minutes) and bounded refresh lifetime (7 days). Inject `TimeProvider`; create access JWTs with `sub`/`ClaimTypes.NameIdentifier`, email, display-name claims, issuer/audience/expiry, and HMAC-SHA256 signing. Generate refresh secrets using `RandomNumberGenerator` (at least 256 bits), send URL-safe opaque text to the caller, and persist only a SHA-256 hex hash.
   - Changes: Implement the application port with `UserManager<ApplicationUser>`, `SignInManager<ApplicationUser>`, and `ApplicationDbContext`. Registration creates `ApplicationUser` with `UserName`/`Email` and `DisplayName`; login uses `CheckPasswordSignInAsync(..., lockoutOnFailure: true)` while returning a uniform failure externally. Registration/login start a new token family and issue a session. `me` loads the current user rather than trusting mutable display data in the JWT.
   - Changes: Rotate in a SQL Server serializable transaction. Locate by hash; atomically transition only an active presented row (`RevokedAtUtc is null && ExpiresAtUtc > now`) to revoked, insert one replacement in the same family, and commit before returning tokens. If the hash is known but revoked/expired, revoke every still-active token in that family in the same transaction, return no session, and treat it as replay. If it is unknown, return no session without revealing why. This permits at most one successful rotation for a presented token; a simultaneous loser revokes the newly issued family token. Logout revokes all active tokens in the supplied token's family when present, and is idempotent externally. Do not log raw credentials, JWTs, cookie values, or refresh values.
   - Acceptance: A single refresh creates exactly one new active token; reuse/concurrent reuse cannot yield a second usable session and invalidates the family; logout prevents later refresh.

4. **Compose bearer authentication, browser-cookie transport, and auth endpoints.**
   - Files: `backend/src/InventoryFlow.Api/Controllers/AuthController.cs`, `backend/src/InventoryFlow.Api/Program.cs`, `backend/src/InventoryFlow.Api/appsettings.json`, `backend/src/InventoryFlow.Api/InventoryFlow.Api.csproj`, `backend/src/InventoryFlow.Api/InventoryFlow.Api.http`, `backend/README.md`, `frontend/.env.example`
   - Changes: Configure the Infrastructure JWT bearer scheme with strict issuer/audience/signature/lifetime validation (zero clock skew unless an explicit operational requirement is approved), then place `UseAuthentication()` before `UseAuthorization()`. Add `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`, and `[Authorize] GET /api/auth/me`. Register/login/refresh return only access-token/session-user JSON; write the raw refresh token to a configured cookie. Refresh/logout read only that cookie, clear it on failure/logout, and never accept a refresh value in JSON or query parameters.
   - Changes: Set the cookie `HttpOnly`, `SameSite=Strict`, `Path=/api/auth`, an explicit bounded `MaxAge`/expiry matching the server refresh lifetime, and `Secure` outside Development (Development must only be used on localhost HTTPS or the documented local HTTP exception). Use a non-secret configurable cookie name. Limit browser deployment to same-site SPA/API origins for this slice; configure exact CORS origins, `AllowCredentials`, headers, and methods—never `AllowAnyOrigin` with credentials. Set the Vite sample API URL to the actual launch profile (`http://localhost:5255`) and the development CORS origin to `http://localhost:5173`. Commit no signing key: add only safe issuer/audience/lifetime/cookie settings, add an API `UserSecretsId`, and document `dotnet user-secrets set "Jwt:SigningKey" ...` plus production environment-variable configuration.
   - Acceptance: Missing/invalid bearer access is 401, a valid login access token can call `/me`, session responses contain no refresh field, Set-Cookie has the required flags, and an allowed credentialed SPA origin can refresh.

5. **Add memory-only SPA session state, API recovery, and authentication pages.**
   - Files: `frontend/src/features/auth/types.ts`, `frontend/src/features/auth/auth-api.ts`, `frontend/src/features/auth/auth-store.ts`, `frontend/src/features/auth/session-bootstrap.tsx`, `frontend/src/features/auth/components/RequireAuth.tsx`, `frontend/src/features/auth/components/PublicOnly.tsx`, `frontend/src/features/auth/pages/LoginPage.tsx`, `frontend/src/features/auth/pages/RegisterPage.tsx`, `frontend/src/features/auth/auth-schema.ts`, `frontend/src/lib/api-client.ts`, `frontend/src/App.tsx`, `frontend/src/app/router/index.tsx`, `frontend/src/components/layout/Topbar.tsx`; new generated primitives only if needed: `frontend/src/components/ui/input.tsx`, `frontend/src/components/ui/label.tsx`
   - Changes: Store access token and authenticated user in non-persisted Zustand state only; do not put either token in localStorage/sessionStorage. Configure Axios `withCredentials: true`, inject the in-memory bearer token on normal requests, and use a separate non-intercepted auth client for refresh. Implement one module-level/single-flight refresh promise and a per-request retry marker: on one 401, refresh once, update the access token, and replay once; never intercept refresh/login/register/logout themselves. On refresh failure, clear state and redirect to `/login` with the requested URL as return state.
   - Changes: Run a single-flight `POST /api/auth/refresh` bootstrap before protected routing (important because React StrictMode otherwise invokes effects twice and could trigger token replay). Guards show a pending state during restoration, redirect unauthenticated workspace users to login, and redirect already-authenticated login/register visitors to `/dashboard`. Add React Hook Form/Zod login and registration forms, display server validation/generic auth errors, navigate to the sanitized in-app return route after success, and retain no password after submit. Make the existing dashboard route tree a child of `RequireAuth`; protect all current placeholder workspace paths too. Replace the hard-coded Topbar identity with returned user data and wire sign-out to the logout endpoint followed by local-state clearing.
   - Acceptance: Direct `/dashboard` loads restore from the HttpOnly cookie or redirects to login; successful login/register returns to the requested workspace route; expired access performs at most one refresh/retry; logout returns the SPA to login and a reload cannot restore the session.

6. **Add focused backend tests using a real relational SQL Server test host and validate the slice.**
   - Files: `backend/tests/InventoryFlow.IntegrationTests/Api/InventoryFlowApiFactory.cs`, `backend/tests/InventoryFlow.IntegrationTests/Api/AuthenticationEndpointsTests.cs`, `backend/tests/InventoryFlow.IntegrationTests/InventoryFlow.IntegrationTests.csproj`, `backend/tests/InventoryFlow.UnitTests/Domain/RefreshTokenTests.cs`, `backend/Directory.Packages.props`
   - Changes: Replace the factory's fake connection-string-only setup with an async SQL Server Testcontainers fixture (including test-only JWT values), apply migrations before clients run, and isolate/reset data per test class/test. Add the direct Infrastructure test project reference only if needed for deterministic database reset/assertions. Do not use EF InMemory: the refresh transaction/concurrency behavior must run against SQL Server. Retain the in-memory data-protection repository override.
   - Changes: Update domain constructor tests for `FamilyId` and add missing-family/expired/revoke-idempotence cases. Add HTTP tests for registration and cookie flags/no refresh JSON, valid/invalid login (uniform 401), `/me` with and without bearer, rotation success, replay rejection plus family invalidation, two simultaneous refreshes of the same cookie (exactly one success and no surviving replacement), logout then refresh rejection, and no plaintext refresh value in the database. Use manually copied cookie headers/independent clients for the concurrent case rather than a shared cookie container.
   - Acceptance: Authentication integration tests exercise the migrated SQL Server schema and pass reliably; all repository quality commands pass: `dotnet build backend/InventoryFlow.sln`, `dotnet test backend/InventoryFlow.sln`, `dotnet format backend/InventoryFlow.sln --verify-no-changes --no-restore`, `bun run typecheck`, `bun run lint`, `bun run build`, and `bunx prettier --check .`.

## Files to Modify
- `backend/Directory.Packages.props` - central JWT bearer and SQL Server Testcontainers package versions.
- `backend/README.md` - local secret/CORS/session configuration and migration instructions.
- `backend/src/InventoryFlow.Api/Program.cs` - authentication middleware ordering and credentialed CORS.
- `backend/src/InventoryFlow.Api/InventoryFlow.Api.csproj` - development user-secrets identifier if not set by the CLI.
- `backend/src/InventoryFlow.Api/appsettings.json` - non-secret JWT/cookie/CORS defaults only.
- `backend/src/InventoryFlow.Api/InventoryFlow.Api.http` - example auth/me requests without committing credentials/tokens.
- `backend/src/InventoryFlow.Api/ExceptionHandling/GlobalExceptionHandler.cs` - FluentValidation RFC 7807 mapping.
- `backend/src/InventoryFlow.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs` - validation pipeline registration.
- `backend/src/InventoryFlow.Domain/Entities/RefreshToken.cs` - family identifier/invariants.
- `backend/src/InventoryFlow.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` - JWT/authentication/options/service registration.
- `backend/src/InventoryFlow.Infrastructure/InventoryFlow.Infrastructure.csproj` - JWT bearer package reference.
- `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs` - family mapping/index.
- `backend/src/InventoryFlow.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs` - generated schema snapshot.
- `backend/tests/InventoryFlow.IntegrationTests/Api/InventoryFlowApiFactory.cs` - relational test fixture/configuration.
- `backend/tests/InventoryFlow.IntegrationTests/InventoryFlow.IntegrationTests.csproj` - Testcontainers and, if necessary, Infrastructure test reference.
- `backend/tests/InventoryFlow.UnitTests/Domain/RefreshTokenTests.cs` - family/expiry lifecycle coverage.
- `frontend/.env.example` - correct local API endpoint.
- `frontend/src/App.tsx` - bootstrap session restoration.
- `frontend/src/app/router/index.tsx` - auth routes and protected workspace tree.
- `frontend/src/lib/api-client.ts` - bearer, credentials, single refresh/retry interceptor.
- `frontend/src/components/layout/Topbar.tsx` - real identity/logout.

## New Files
- `backend/src/InventoryFlow.Application/Features/Authentication/*` - auth commands, validators, models, handlers, and Infrastructure-facing port.
- `backend/src/InventoryFlow.Application/Common/Behaviors/ValidationBehavior.cs` - MediatR FluentValidation behavior.
- `backend/src/InventoryFlow.Infrastructure/Authentication/JwtOptions.cs` - validated non-secret JWT/cookie options model.
- `backend/src/InventoryFlow.Infrastructure/Authentication/JwtAccessTokenIssuer.cs` - JWT construction.
- `backend/src/InventoryFlow.Infrastructure/Authentication/RefreshTokenGenerator.cs` - CSPRNG opaque-secret/hash functions.
- `backend/src/InventoryFlow.Infrastructure/Authentication/IdentityAuthenticationService.cs` - Identity/database-backed application port implementation and rotation transaction.
- `backend/src/InventoryFlow.Api/Controllers/AuthController.cs` - HTTP/cookie boundary for auth endpoints.
- `backend/src/InventoryFlow.Infrastructure/Persistence/Migrations/<timestamp>_AddRefreshTokenFamilies.cs` and `.Designer.cs` - generated additive migration.
- `backend/tests/InventoryFlow.IntegrationTests/Api/AuthenticationEndpointsTests.cs` - end-to-end authentication/replay/concurrency coverage.
- `frontend/src/features/auth/{types.ts,auth-api.ts,auth-store.ts,auth-schema.ts,session-bootstrap.tsx}` - session model, transport, validation, and restoration.
- `frontend/src/features/auth/components/{RequireAuth.tsx,PublicOnly.tsx}` - route guards.
- `frontend/src/features/auth/pages/{LoginPage.tsx,RegisterPage.tsx}` - auth forms.
- `frontend/src/components/ui/{input.tsx,label.tsx}` - only if generated from the existing shadcn registry for the forms.

## Dependencies
1. Task 1 establishes the Application port/contracts used by Tasks 3 and 4.
2. Task 2 must land before Task 3 so service code and tests operate on the family-aware schema.
3. Task 3 depends on Tasks 1–2; Task 4 depends on Task 3’s options/service registrations.
4. Task 5 depends on the finalized API/cookie response contract from Task 4.
5. Task 6 depends on Tasks 2–4; run frontend static checks after Task 5.

## Risks
- **High — replay semantics vs. multiple tabs:** Family revocation deliberately invalidates a newly rotated token if the old cookie is concurrently reused. The SPA single-flight logic prevents same-tab races, but separate tabs can sign each other out; this is the secure, documented P0 trade-off rather than adding a grace window that weakens replay detection.
- **High — secret/deployment configuration:** The application must fail startup without a strong `Jwt:SigningKey`; it must be supplied through user secrets locally and a secret manager/environment in deployment. Do not add it to `appsettings.json`, `.env.example`, test fixtures outside process memory, or logs.
- **High — cookie/CORS topology:** `SameSite=Strict` is appropriate only for same-site SPA/API deployment. If product later requires genuinely cross-site cookies, add explicit CSRF protection and reassess cookie settings; do not loosen to `SameSite=None` in this P0.
- **Medium — integration environment:** SQL Server Testcontainers requires Docker in CI/developer environments. It is necessary to verify serializable transactions; do not substitute EF InMemory for concurrency tests.
- **Medium — registration policy:** This plan assumes approved open self-service registration. If product requires invite-only/seeding, reject registration until that policy is supplied; do not introduce roles, invitations, or tenancy into P0.
- **Medium — access-token revocation boundary:** Replay/logout invalidates the refresh family, not already-issued stateless JWTs; the 10-minute access lifetime bounds that window. Immediate JWT deny-listing is explicitly out of scope.
- **Low — dashboard data:** Current dashboard metrics remain static presentation data and are not an authentication API contract; defer real inventory integration to the catalog/movement slices.

## Review Findings
- **High:** `backend/src/InventoryFlow.Api/Program.cs:15-18,44-49` registers authorization but neither JWT authentication services nor `UseAuthentication()`, so bearer principals cannot be established.
- **High:** `frontend/src/app/router/index.tsx:23-45` exposes the complete dashboard/workspace route tree publicly.
- **High:** `backend/src/InventoryFlow.Domain/Entities/RefreshToken.cs:10-92` and `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs:11-35` support one-token revocation but lack a family identifier/index, so replay cannot revoke descendant rotations.
- **Medium:** `backend/tests/InventoryFlow.IntegrationTests/Api/InventoryFlowApiFactory.cs:10-58` only sets an unreachable SQL Server connection string and currently never migrates/uses a database; it cannot prove token persistence or transaction behavior.
- **Medium:** `backend/src/InventoryFlow.Api/appsettings.json:2-4` has no SPA CORS origin, while `frontend/.env.example:1` targets port 5000 instead of the API launch profile’s port 5255.
- **Medium:** `frontend/src/components/layout/Topbar.tsx:89-99` displays a hard-coded user and inert sign-out action.

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "The Review Findings section gives severity, exact paths and line ranges; Tasks 1-6 name implementation, migration, package, configuration, frontend, and test files; Risks supplies residual risks."
    }
  ],
  "changedFiles": [
    ".pi-subagents/artifacts/outputs/65ebbd79-8d00-420c-9b17-ecb25ac38889/plan.md"
  ],
  "testsAddedOrUpdated": [
    "backend/tests/InventoryFlow.IntegrationTests/Api/AuthenticationEndpointsTests.cs (planned)",
    "backend/tests/InventoryFlow.UnitTests/Domain/RefreshTokenTests.cs (planned)"
  ],
  "commandsRun": [
    {
      "command": "repository and auth-review artifact inspection",
      "result": "passed",
      "summary": "Read actual backend, frontend, persistence, test-host, package, and scout-review files; no project source was edited."
    },
    {
      "command": "dotnet build/test/format and frontend typecheck/lint/build/prettier",
      "result": "not-run",
      "summary": "Planning-only task; these are listed as implementation validation gates."
    }
  ],
  "validationOutput": [
    "Auth review artifact was inspected at .pi-subagents/artifacts/outputs/ba19ad26-5a56-40c6-8e45-0db7843b77e2/parallel-0/0-scout/context.md.",
    "No project files were modified; only this required planning artifact was written."
  ],
  "residualRisks": [
    "Concurrent independent browser tabs can trigger intentional family revocation when both rotate the same cookie.",
    "Strong JWT secret, exact production origins, and same-site deployment must be configured outside committed files.",
    "SQL Server Testcontainers validation requires Docker.",
    "Open self-service registration remains a product-policy assumption."
  ],
  "noStagedFiles": true,
  "diffSummary": "Planning artifact only; no application implementation was performed.",
  "reviewFindings": [
    "high: backend/src/InventoryFlow.Api/Program.cs:15-18,44-49 - authorization exists without authentication registration or UseAuthentication middleware.",
    "high: frontend/src/app/router/index.tsx:23-45 - all workspace routes are publicly accessible.",
    "high: backend/src/InventoryFlow.Domain/Entities/RefreshToken.cs:10-92 - refresh token persistence has no family identifier for replay-family revocation.",
    "medium: backend/tests/InventoryFlow.IntegrationTests/Api/InventoryFlowApiFactory.cs:10-58 - current host cannot test SQL-backed persistence/transaction behavior.",
    "medium: backend/src/InventoryFlow.Api/appsettings.json:2-4 and frontend/.env.example:1 - CORS has no SPA origin and frontend’s sample API port mismatches launch settings."
  ],
  "manualNotes": "The plan deliberately excludes roles/permissions, recovery, tenancy, catalog/dashboard data work, and a new frontend test framework."
}
```