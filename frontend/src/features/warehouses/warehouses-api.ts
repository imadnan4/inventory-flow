import { apiClient } from "@/lib/api-client"
import type { CreateWarehouseInput, Warehouse } from "./types"
export const listWarehouses = () =>
  apiClient.get<Warehouse[]>("/api/warehouses").then((x) => x.data)
export const createWarehouse = (x: CreateWarehouseInput) =>
  apiClient.post<Warehouse>("/api/warehouses", x).then((x) => x.data)
export const archiveWarehouse = (id: string) =>
  apiClient.delete(`/api/warehouses/${id}`)
