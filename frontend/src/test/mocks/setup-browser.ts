import { worker } from "../mocks/browser"

if (typeof window !== "undefined") {
  worker.start({
    onUnhandledRequest: "error",
  })
}