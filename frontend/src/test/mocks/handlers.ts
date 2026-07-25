import { http, HttpResponse } from "msw"

export const handlers = [
  http.get("/api/auth/refresh", () => {
    return HttpResponse.json({
      accessToken: "test-token",
      refreshToken: "test-refresh-token",
    })
  }),
]