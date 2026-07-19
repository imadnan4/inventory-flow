export type WorkspaceRole = "Owner" | "Member"
export type AuthenticatedWorkspace = {
  id: string
  name: string
  role: WorkspaceRole
}
export type AuthenticatedUser = {
  id: string
  email: string
  displayName: string
  workspace: AuthenticatedWorkspace
  workspaces: AuthenticatedWorkspace[]
}
export type AuthenticationResponse = {
  accessToken: string
  accessTokenExpiresAtUtc: string
  user: AuthenticatedUser
}
