import { Outlet } from "react-router"

import { Sidebar } from "@/components/layout/Sidebar"
import { Topbar } from "@/components/layout/Topbar"
import { cn } from "@/lib/utils"
import { useUiStore } from "@/store/ui-store"

export function DashboardLayout() {
  const isSidebarCollapsed = useUiStore((state) => state.isSidebarCollapsed)

  return (
    <div className="min-h-svh bg-muted/30">
      <Sidebar />
      <div className={cn(isSidebarCollapsed ? "lg:pl-20" : "lg:pl-64")}>
        <Topbar />
        <main className="mx-auto w-full max-w-screen-2xl p-4 sm:p-6 lg:p-8">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
