import { apiClient } from "@/lib/api-client"
import type {
  PurchaseReceipt,
  RecordPurchaseReceiptPayload,
} from "@/features/purchases/types"

export const listPurchaseReceipts = () =>
  apiClient
    .get<PurchaseReceipt[]>("/api/purchases/receipts")
    .then((response) => response.data)

export const recordPurchaseReceipt = (payload: RecordPurchaseReceiptPayload) =>
  apiClient
    .post<PurchaseReceipt>("/api/purchases/receipts", payload, {
      headers: { "Idempotency-Key": payload.idempotencyKey },
    })
    .then((response) => response.data)
