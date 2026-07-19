import { z } from "zod"

const decimalQuantity = z
  .string()
  .regex(
    /^(?:0|[1-9]\d{0,13})(?:\.\d{1,4})?$/,
    "Quantity must be a positive decimal with up to four decimal places."
  )
  .refine((value) => Number(value) > 0, "Quantity must be greater than zero.")

export const warehouseTransferSchema = z
  .object({
    sourceWarehouseId: z.string().uuid("Choose a source warehouse."),
    destinationWarehouseId: z.string().uuid("Choose a destination warehouse."),
    productId: z.string().uuid("Choose a product."),
    quantity: decimalQuantity,
  })
  .refine((value) => value.sourceWarehouseId !== value.destinationWarehouseId, {
    message: "Source and destination warehouses must be different.",
    path: ["destinationWarehouseId"],
  })
