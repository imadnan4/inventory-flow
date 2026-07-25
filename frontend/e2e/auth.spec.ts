import { test, expect } from "@playwright/test"

test.describe("Authentication", () => {
  test("login page renders", async ({ page }) => {
    await page.route("**/api/auth/refresh", async (route) => {
      await route.fulfill({ status: 401 })
    })
    await page.goto("/login")
    await expect(page.getByRole("heading", { name: /sign in/i })).toBeVisible()
  })

  test("redirects unauthenticated users on protected route", async ({ page }) => {
    await page.route("**/api/auth/refresh", async (route) => {
      await route.fulfill({ status: 401 })
    })
    await page.goto("/dashboard")
    await expect(page).toHaveURL(/\/login/)
  })
})