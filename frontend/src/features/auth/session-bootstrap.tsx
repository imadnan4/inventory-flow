import { useEffect } from "react"
import { restoreSession } from "@/lib/api-client"
let bootstrap: Promise<void> | undefined
export function SessionBootstrap() {
  useEffect(() => {
    bootstrap ??= restoreSession()
  }, [])
  return null
}
