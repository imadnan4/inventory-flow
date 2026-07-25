import { test, expect } from "@playwright/test"

test.describe("Authentication", () => {
  test("login page renders", async ({ page }) => {
    await page.goto("/auth/login")
    await expect(page.getByRole("heading", { name: /login/i })).toBeVisible()
  })

  test("redirects unauthenticated users on protected route", async ({ page }) => {
    await page.goto("/")
    await page.waitForURL(/\/auth\/login/)
    await expect(page).toHaveURL(/\/auth\/login/)
  })
})