export type SalesFulfillment = {
  id: string
  warehouseId: string
  productId: string
  quantity: number
  inventoryMovementId: string
  fulfilledAtUtc: string
}

export type RecordSalesFulfillmentPayload = {
  warehouseId: string
  productId: string
  quantity: string
  idempotencyKey: string
}
