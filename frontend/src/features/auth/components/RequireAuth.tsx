import { Navigate, Outlet, useLocation } from "react-router"
import { useAuthStore } from "@/features/auth/auth-store"
export function RequireAuth() {
  const { accessToken, isRestoring } = useAuthStore()
  const location = useLocation()
  if (isRestoring)
    return (
      <main className="grid min-h-svh place-items-center">
        Restoring session…
      </main>
    )
  return accessToken ? (
    <Outlet />
  ) : (
    <Navigate
      replace
      state={{ from: `${location.pathname}${location.search}${location.hash}` }}
      to="/login"
    />
  )
}
