import { apiClient } from "@/lib/api-client"
import type { CreateProductPayload, Product } from "@/features/products/types"

export const listProducts = () =>
  apiClient.get<Product[]>("/api/products").then((response) => response.data)
export const createProduct = (payload: CreateProductPayload) =>
  apiClient
    .post<Product>("/api/products", payload)
    .then((response) => response.data)
export const archiveProduct = (id: string) =>
  apiClient.delete(`/api/products/${id}`)
