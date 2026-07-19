export type Product = {
  id: string
  name: string
  sku: string
  createdAtUtc: string
}

export type CreateProductPayload = {
  name: string
  sku: string
}
