import { z } from "zod"

export const productSchema = z.object({
  name: z.string().trim().min(1, "Name is required.").max(200),
  sku: z.string().trim().min(1, "SKU is required.").max(100),
})
