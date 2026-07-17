import {
  DashboardSquare01Icon,
  DeliveryTruckIcon,
  Layers01Icon,
  PackageIcon,
  Settings01Icon,
  ShoppingCart01Icon,
  UserGroupIcon,
  WarehouseIcon,
} from "@hugeicons/core-free-icons"
import { HugeiconsIcon, type IconSvgElement } from "@hugeicons/react"
import { NavLink } from "react-router"

import { cn } from "@/lib/utils"
import { useUiStore } from "@/store/ui-store"

type NavigationItem = {
  label: string
  to: string
  icon: IconSvgElement
}

const navigationItems: NavigationItem[] = [
  { label: "Dashboard", to: "/dashboard", icon: DashboardSquare01Icon },
  { label: "Inventory", to: "/inventory", icon: Layers01Icon },
  { label: "Products", to: "/products", icon: PackageIcon },
  { label: "Categories", to: "/categories", icon: Layers01Icon },
  { label: "Suppliers", to: "/suppliers", icon: DeliveryTruckIcon },
  { label: "Warehouses", to: "/warehouses", icon: WarehouseIcon },
  { label: "Purchases", to: "/purchases", icon: ShoppingCart01Icon },
  { label: "Sales", to: "/sales", icon: ShoppingCart01Icon },
  { label: "Reports", to: "/reports", icon: DashboardSquare01Icon },
  { label: "Users", to: "/users", icon: UserGroupIcon },
  { label: "Settings", to: "/settings", icon: Settings01Icon },
]

type NavigationLinksProps = {
  collapsed?: boolean
  onNavigate?: () => void
}

export function NavigationLinks({
  collapsed = false,
  onNavigate,
}: NavigationLinksProps) {
  return (
    <nav aria-label="Main navigation" className="space-y-1">
      {navigationItems.map((item) => (
        <NavLink
          className={({ isActive }) =>
            cn(
              "flex h-10 items-center gap-3 rounded-lg px-3 text-sm font-medium text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground",
              isActive &&
                "bg-primary text-primary-foreground hover:bg-primary/90 hover:text-primary-foreground",
              collapsed && "justify-center px-0"
            )
          }
          key={item.to}
          onClick={onNavigate}
          to={item.to}
          title={collapsed ? item.label : undefined}
        >
          <HugeiconsIcon icon={item.icon} size={20} strokeWidth={1.8} />
          {collapsed ? (
            <span className="sr-only">{item.label}</span>
          ) : (
            item.label
          )}
        </NavLink>
      ))}
    </nav>
  )
}

export function Sidebar() {
  const isSidebarCollapsed = useUiStore((state) => state.isSidebarCollapsed)

  return (
    <aside
      className={cn(
        "fixed inset-y-0 left-0 z-30 hidden border-r bg-background lg:flex lg:flex-col",
        isSidebarCollapsed ? "w-20" : "w-64"
      )}
    >
      <div className="flex h-16 items-center gap-3 border-b px-5">
        <div className="grid size-8 place-items-center rounded-lg bg-primary text-primary-foreground">
          <HugeiconsIcon icon={PackageIcon} size={18} strokeWidth={2} />
        </div>
        {isSidebarCollapsed ? null : (
          <span className="font-semibold tracking-tight">Inventory Flow</span>
        )}
      </div>
      <div className="flex-1 overflow-y-auto p-3">
        <NavigationLinks collapsed={isSidebarCollapsed} />
      </div>
      <div className="border-t p-3">
        <p
          className={cn(
            "px-3 text-xs text-muted-foreground",
            isSidebarCollapsed && "sr-only"
          )}
        >
          © 2026 Inventory Flow
        </p>
      </div>
    </aside>
  )
}
