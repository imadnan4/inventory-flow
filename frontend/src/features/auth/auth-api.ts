import { apiClient, authClient } from "@/lib/api-client"
import type {
  AuthenticationResponse,
  AuthenticatedUser,
} from "@/features/auth/types"

export const register = (payload: {
  displayName: string
  email: string
  password: string
}) =>
  authClient
    .post<AuthenticationResponse>("/api/auth/register", payload)
    .then((response) => response.data)
export const login = (payload: { email: string; password: string }) =>
  authClient
    .post<AuthenticationResponse>("/api/auth/login", payload)
    .then((response) => response.data)
export const refresh = () =>
  authClient
    .post<AuthenticationResponse>("/api/auth/refresh")
    .then((response) => response.data)
export const switchWorkspace = (workspaceId: string) =>
  apiClient
    .post<AuthenticationResponse>("/api/auth/workspace/switch", { workspaceId })
    .then((response) => response.data)
export const logout = () => authClient.post("/api/auth/logout")
export const getCurrentUser = () =>
  apiClient
    .get<AuthenticatedUser>("/api/auth/me")
    .then((response) => response.data)
