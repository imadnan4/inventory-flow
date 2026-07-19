import { apiClient } from "@/lib/api-client"
import type {
  RecordWarehouseTransferPayload,
  WarehouseTransfer,
} from "@/features/transfers/types"

export const listWarehouseTransfers = () =>
  apiClient
    .get<WarehouseTransfer[]>("/api/transfers")
    .then((response) => response.data)

export const recordWarehouseTransfer = (
  payload: RecordWarehouseTransferPayload
) =>
  apiClient
    .post<WarehouseTransfer>("/api/transfers", payload, {
      headers: { "Idempotency-Key": payload.idempotencyKey },
    })
    .then((response) => response.data)
