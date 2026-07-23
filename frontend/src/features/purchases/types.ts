export type PurchaseReceipt = {
  id: string
  supplierId: string
  warehouseId: string
  productId: string
  quantity: number
  inventoryMovementId: string
  receivedAtUtc: string
}

export type RecordPurchaseReceiptPayload = {
  supplierId: string
  warehouseId: string
  productId: string
  quantity: string
  idempotencyKey: string
}
