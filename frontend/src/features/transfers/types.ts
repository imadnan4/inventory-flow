export type WarehouseTransfer = {
  id: string
  sourceWarehouseId: string
  destinationWarehouseId: string
  productId: string
  quantity: number
  sourceInventoryMovementId: string
  destinationInventoryMovementId: string
  transferredAtUtc: string
}

export type RecordWarehouseTransferPayload = {
  sourceWarehouseId: string
  destinationWarehouseId: string
  productId: string
  quantity: string
  idempotencyKey: string
}
