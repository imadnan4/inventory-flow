import { apiClient } from "@/lib/api-client"
import type {
  CreateSupplierPayload,
  Supplier,
} from "@/features/suppliers/types"

export const listSuppliers = () =>
  apiClient.get<Supplier[]>("/api/suppliers").then((response) => response.data)
export const createSupplier = (payload: CreateSupplierPayload) =>
  apiClient
    .post<Supplier>("/api/suppliers", payload)
    .then((response) => response.data)
export const archiveSupplier = (id: string) =>
  apiClient.delete(`/api/suppliers/${id}`)
