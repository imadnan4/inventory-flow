import axios, { type AxiosError, type InternalAxiosRequestConfig } from "axios"
import { environment } from "@/app/config/environment"
import { useAuthStore } from "@/features/auth/auth-store"

export const authClient = axios.create({
  baseURL: environment.apiBaseUrl,
  withCredentials: true,
  headers: { "Content-Type": "application/json" },
})
export const apiClient = axios.create({
  baseURL: environment.apiBaseUrl,
  withCredentials: true,
  headers: { "Content-Type": "application/json" },
})

let refreshPromise: Promise<string | null> | null = null
let sessionEpoch = 0

export const invalidateSession = () => {
  sessionEpoch += 1
  useAuthStore.getState().clearSession()
}

const refreshAccessToken = () => {
  if (!refreshPromise) {
    const refreshEpoch = sessionEpoch
    refreshPromise = authClient
      .post("/api/auth/refresh")
      .then((response) => {
        if (refreshEpoch !== sessionEpoch) return null

        useAuthStore.getState().setSession(response.data)
        return response.data.accessToken as string
      })
      .catch(() => {
        if (refreshEpoch === sessionEpoch) invalidateSession()
        return null
      })
      .finally(() => {
        refreshPromise = null
      })
  }
  return refreshPromise
}

apiClient.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})
apiClient.interceptors.response.use(undefined, async (error: AxiosError) => {
  const request = error.config as
    (InternalAxiosRequestConfig & { _authRetried?: boolean }) | undefined
  if (error.response?.status !== 401 || !request || request._authRetried)
    return Promise.reject(error)
  request._authRetried = true
  const token = await refreshAccessToken()
  if (!token) return Promise.reject(error)
  request.headers.Authorization = `Bearer ${token}`
  return apiClient(request)
})

export const restoreSession = async () => {
  const restoreEpoch = sessionEpoch

  try {
    const session = await authClient
      .post("/api/auth/refresh")
      .then((response) => response.data)

    if (restoreEpoch === sessionEpoch)
      useAuthStore.getState().setSession(session)
  } catch {
    if (restoreEpoch === sessionEpoch) invalidateSession()
  } finally {
    useAuthStore.getState().finishRestoring()
  }
}
