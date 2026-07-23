import { RouterProvider } from "react-router"

import { router } from "@/app/router"
import { SessionBootstrap } from "@/features/auth/session-bootstrap"

export default function App() {
  return (
    <>
      <SessionBootstrap />
      <RouterProvider router={router} />
    </>
  )
}
