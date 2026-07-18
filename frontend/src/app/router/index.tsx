import { Navigate, createBrowserRouter } from "react-router"

import { DashboardLayout } from "@/layouts/DashboardLayout"
import { PublicOnly } from "@/features/auth/components/PublicOnly"
import { RequireAuth } from "@/features/auth/components/RequireAuth"

const placeholderPage = (title: string) => (
  <section className="rounded-xl border border-dashed p-8">
    <h1 className="text-xl font-semibold">{title}</h1>
    <p className="mt-2 text-sm text-muted-foreground">
      This feature is planned for a dedicated vertical slice.
    </p>
  </section>
)

const notFoundPage = (
  <main className="grid min-h-svh place-items-center p-6">
    <div className="text-center">
      <p className="text-sm font-medium text-muted-foreground">404</p>
      <h1 className="mt-2 text-2xl font-semibold">Page not found</h1>
    </div>
  </main>
)

export const router = createBrowserRouter([
  {
    element: <PublicOnly />,
    children: [
      { path: "login", lazy: () => import("@/features/auth/pages/LoginPage") },
      {
        path: "register",
        lazy: () => import("@/features/auth/pages/RegisterPage"),
      },
    ],
  },
  {
    element: <RequireAuth />,
    children: [
      {
        path: "/",
        element: <DashboardLayout />,
        children: [
          { index: true, element: <Navigate replace to="/dashboard" /> },
          {
            path: "dashboard",
            lazy: () => import("@/features/dashboard/pages/DashboardPage"),
          },
          { path: "products", element: placeholderPage("Products") },
          { path: "categories", element: placeholderPage("Categories") },
          { path: "inventory", element: placeholderPage("Inventory") },
          { path: "suppliers", element: placeholderPage("Suppliers") },
          { path: "warehouses", element: placeholderPage("Warehouses") },
          { path: "purchases", element: placeholderPage("Purchase orders") },
          { path: "sales", element: placeholderPage("Sales orders") },
          { path: "reports", element: placeholderPage("Reports") },
          { path: "users", element: placeholderPage("Users") },
          { path: "settings", element: placeholderPage("Settings") },
        ],
      },
    ],
  },
  { path: "*", element: notFoundPage },
])
