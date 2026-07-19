const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "/"

export const environment = {
  apiBaseUrl,
} as const
