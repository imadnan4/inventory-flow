import { apiClient } from "@/lib/api-client"
import type {
  InventoryBalance,
  InventoryMovement,
  RecordInventoryMovementPayload,
} from "@/features/inventory/types"

type BalanceFilters = {
  warehouseId?: string
  productId?: string
}

export const listInventoryBalances = (filters: BalanceFilters) =>
  apiClient
    .get<InventoryBalance[]>("/api/inventory/balances", { params: filters })
    .then((response) => response.data)

export const recordInventoryMovement = (
  kind: "receipt" | "issue",
  payload: RecordInventoryMovementPayload
) =>
  apiClient
    .post<InventoryMovement>(`/api/inventory/${kind}s`, payload, {
      headers: { "Idempotency-Key": payload.idempotencyKey },
    })
    .then((response) => response.data)
