Inherited decisions:
- Authentication is complete; the roadmap places catalog management next.
- The product is positioned as a SaaS operating “from one workspace.”
- Current identity is user-only; authorization resolves only `ApplicationUser`.

Diagnosis:
- Building products directly with `ApplicationUser.Id` would turn the first user model into the tenancy boundary.
- That makes later shared workspaces, invitations, role enforcement, and cross-user catalog access a migration and authorization retrofit.

Drift / contradiction check:
- **Blocker — `README.md:3,112`:** “workspace” SaaS positioning has no workspace persistence model.
- **Blocker — `ApplicationUser.cs:8-16`, `ApplicationDbContext.cs:12-29`:** no Workspace or membership entity/relationship exists.
- **High — `AuthController.cs:61-68`:** current-user authorization supplies only a user identity; it cannot resolve or enforce a workspace boundary.
- **High — `frontend/src/app/router/index.tsx:25-54`:** the Products route is a placeholder; no current-workspace state or switching contract exists.

Recommendation:
- Do **not** implement an owner-scoped catalog now.
- First deliver minimal tenancy foundation:
  1. `Workspace` and `WorkspaceMember` persistence with an Owner role.
  2. Create a workspace and owner membership during registration.
  3. Resolve one current workspace server-side for authenticated requests.
  4. Require all later catalog queries/mutations to filter and write `WorkspaceId`.
  5. Defer invitations, workspace switching, granular permissions, and UI administration.
- This is the smallest path that preserves data isolation without delaying catalog value unnecessarily.

Risks:
- The self-registration/provisioning model is product-sensitive.
- Multi-workspace users require an explicit current-workspace selection and persistence contract; silently picking one is unsafe.
- Workspace deletion, invitations, and role hierarchy should remain out of this foundational slice.

Need from main agent:
- Confirm either:
  - **Recommended default:** self-registration creates one personal workspace; a user has one workspace for now.
  - **Alternative:** workspaces are provisioned/invitation-only, or users need multiple workspaces immediately.

Suggested execution prompt:
- No implementation handoff until the workspace provisioning and multi-workspace decisions are confirmed.