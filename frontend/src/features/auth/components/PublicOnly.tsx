import { Navigate, Outlet } from "react-router"
import { useAuthStore } from "@/features/auth/auth-store"
export function PublicOnly() {
  const { accessToken, isRestoring } = useAuthStore()
  if (isRestoring)
    return (
      <main className="grid min-h-svh place-items-center">
        Restoring session…
      </main>
    )
  return accessToken ? <Navigate replace to="/dashboard" /> : <Outlet />
}
