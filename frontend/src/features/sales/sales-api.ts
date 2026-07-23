import { apiClient } from "@/lib/api-client"
import type {
  RecordSalesFulfillmentPayload,
  SalesFulfillment,
} from "@/features/sales/types"

export const listSalesFulfillments = () =>
  apiClient
    .get<SalesFulfillment[]>("/api/sales/fulfillments")
    .then((response) => response.data)

export const recordSalesFulfillment = (
  payload: RecordSalesFulfillmentPayload
) =>
  apiClient
    .post<SalesFulfillment>("/api/sales/fulfillments", payload, {
      headers: { "Idempotency-Key": payload.idempotencyKey },
    })
    .then((response) => response.data)
