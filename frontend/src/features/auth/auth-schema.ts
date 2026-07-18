import { z } from "zod"
export const loginSchema = z.object({
  email: z.email("Enter a valid email address."),
  password: z.string().min(1, "Password is required."),
})
export const registerSchema = loginSchema.extend({
  displayName: z.string().trim().min(1, "Display name is required.").max(200),
  password: z.string().min(1, "Password is required."),
})
