import { z } from "zod"

export const supplierSchema = z.object({
  name: z.string().trim().min(1, "Name is required.").max(200),
})
