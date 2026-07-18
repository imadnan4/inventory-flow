import { z } from "zod"

const decimalQuantity = z
  .string()
  .regex(
    /^(?:0|[1-9]\d{0,13})(?:\.\d{1,4})?$/,
    "Quantity must be a positive decimal with up to four decimal places."
  )
  .refine((value) => Number(value) > 0, "Quantity must be greater than zero.")

export const inventoryMovementSchema = z.object({
  warehouseId: z.string().uuid("Choose a warehouse."),
  productId: z.string().uuid("Choose a product."),
  quantity: decimalQuantity,
})
