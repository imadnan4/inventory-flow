export type AuthenticatedWorkspace = {
  id: string
  name: string
}
export type AuthenticatedUser = {
  id: string
  email: string
  displayName: string
  workspace: AuthenticatedWorkspace
}
export type AuthenticationResponse = {
  accessToken: string
  accessTokenExpiresAtUtc: string
  user: AuthenticatedUser
}
