export type AuthenticatedUser = {
  id: string
  email: string
  displayName: string
}
export type AuthenticationResponse = {
  accessToken: string
  accessTokenExpiresAtUtc: string
  user: AuthenticatedUser
}
