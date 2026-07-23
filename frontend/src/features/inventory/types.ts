export type InventoryBalance = {
  warehouseId: string
  productId: string
  quantity: number
}

export type InventoryMovement = {
  id: string
  warehouseId: string
  productId: string
  type: number
  quantity: number
  balanceAfterQuantity: number
  occurredAtUtc: string
}

export type RecordInventoryMovementPayload = {
  warehouseId: string
  productId: string
  quantity: string
  idempotencyKey: string
}
