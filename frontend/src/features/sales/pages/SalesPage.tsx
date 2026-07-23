import { useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import { PageHeader } from "@/components/shared/PageHeader"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { useAuthStore } from "@/features/auth/auth-store"
import { listProducts } from "@/features/products/products-api"
import {
  listSalesFulfillments,
  recordSalesFulfillment,
} from "@/features/sales/sales-api"
import { inventoryBalancesKeyPrefix } from "@/features/inventory/inventory-queries"
import { salesFulfillmentSchema } from "@/features/sales/sales-schema"
import type { RecordSalesFulfillmentPayload } from "@/features/sales/types"
import { listWarehouses } from "@/features/warehouses/warehouses-api"

const key = (name: string, userId: string, workspaceId: string) =>
  [name, userId, workspaceId] as const
const readError = (error: unknown) => {
  if (typeof error === "object" && error && "response" in error) {
    const data = (
      error as { response?: { data?: { detail?: string; title?: string } } }
    ).response?.data
    return data?.detail ?? data?.title ?? "Unable to fulfill the sale."
  }
  return "Unable to fulfill the sale."
}

export function Component() {
  const queryClient = useQueryClient()
  const user = useAuthStore((state) => state.user)
  const userId = user?.id ?? "anonymous"
  const workspaceId = user?.workspace.id ?? "none"
  const [warehouseId, setWarehouseId] = useState("")
  const [productId, setProductId] = useState("")
  const [quantity, setQuantity] = useState("")
  const [formError, setFormError] = useState("")
  const [retryFulfillment, setRetryFulfillment] =
    useState<RecordSalesFulfillmentPayload | null>(null)

  const clearRetry = () => { if (retryFulfillment) setRetryFulfillment(null) }
  const warehouses = useQuery({
    queryKey: key("warehouses", userId, workspaceId),
    queryFn: listWarehouses,
    enabled: user !== null,
  })
  const products = useQuery({
    queryKey: key("products", userId, workspaceId),
    queryFn: listProducts,
    enabled: user !== null,
  })
  const fulfillments = useQuery({
    queryKey: key("sales-fulfillments", userId, workspaceId),
    queryFn: listSalesFulfillments,
    enabled: user !== null,
  })
  const mutation = useMutation({
    mutationKey: key("sales-fulfillments", userId, workspaceId),
    mutationFn: recordSalesFulfillment,
    onSuccess: () => {
      setFormError("")
      setRetryFulfillment(null)
      setQuantity("")
      queryClient.invalidateQueries({
        queryKey: key("sales-fulfillments", userId, workspaceId),
      })
      queryClient.invalidateQueries({
        queryKey: inventoryBalancesKeyPrefix(userId, workspaceId),
      })
    },
    onError: (error, variables) => {
      setRetryFulfillment(variables)
      setFormError(readError(error))
    },
  })
  const submit = () => {
    const parsed = salesFulfillmentSchema.safeParse({
      warehouseId,
      productId,
      quantity,
    })
    if (!parsed.success) {
      setFormError(
        parsed.error.issues[0]?.message ?? "Check the fulfillment details."
      )
      return
    }
    setFormError("")
    mutation.mutate({ ...parsed.data, idempotencyKey: crypto.randomUUID() })
  }
  const loading = warehouses.isLoading || products.isLoading
  const names = {
    warehouses: new Map(warehouses.data?.map((x) => [x.id, x.name])),
    products: new Map(products.data?.map((x) => [x.id, x.name])),
  }
  const disabled = loading || mutation.isPending
  return (
    <div className="space-y-6">
      <PageHeader
        title="Sales"
        description="Fulfill single-line sales directly from inventory."
      />
      <div className="grid gap-6 xl:grid-cols-[22rem_minmax(0,1fr)]">
        <Card>
          <CardHeader>
            <CardTitle>Fulfill sale</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-4">
              {loading ? (
                <p className="text-muted-foreground">Loading catalog…</p>
              ) : null}
              {warehouses.isError || products.isError ? (
                <p className="text-destructive" role="alert">
                  {readError(warehouses.error ?? products.error)}
                </p>
              ) : null}
              <label className="block text-sm">
                Warehouse
                <select
                  className="mt-1 w-full rounded-md border bg-background p-2"
                  disabled={disabled}
                  onChange={(e) => { setWarehouseId(e.target.value); clearRetry() }}
                  value={warehouseId}
                >
                  <option value="">Choose a warehouse</option>
                  {warehouses.data?.map((x) => (
                    <option key={x.id} value={x.id}>
                      {x.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="block text-sm">
                Product
                <select
                  className="mt-1 w-full rounded-md border bg-background p-2"
                  disabled={disabled}
                  onChange={(e) => { setProductId(e.target.value); clearRetry() }}
                  value={productId}
                >
                  <option value="">Choose a product</option>
                  {products.data?.map((x) => (
                    <option key={x.id} value={x.id}>
                      {x.name} ({x.sku})
                    </option>
                  ))}
                </select>
              </label>
              <label className="block text-sm">
                Quantity
                <input
                  className="mt-1 w-full rounded-md border bg-background p-2"
                  disabled={disabled}
                  inputMode="decimal"
                  onChange={(e) => { setQuantity(e.target.value); clearRetry() }}
                  placeholder="0.0000"
                  value={quantity}
                />
              </label>
              {formError ? (
                <p className="text-sm text-destructive" role="alert">
                  {formError}
                </p>
              ) : null}
              {retryFulfillment ? (
                <Button
                  disabled={mutation.isPending}
                  onClick={() => mutation.mutate(retryFulfillment)}
                  type="button"
                  variant="outline"
                >
                  Retry fulfillment
                </Button>
              ) : null}
              <Button disabled={disabled} onClick={submit} type="button">
                {mutation.isPending ? "Fulfilling…" : "Fulfill sale"}
              </Button>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Fulfillment history</CardTitle>
          </CardHeader>
          <CardContent>
            {fulfillments.isLoading ? (
              <p className="text-muted-foreground">Loading fulfillments…</p>
            ) : null}
            {fulfillments.isError ? (
              <p className="text-destructive" role="alert">
                {readError(fulfillments.error)}
              </p>
            ) : null}
            {fulfillments.data?.length === 0 ? (
              <p className="text-muted-foreground">
                No sales fulfillments yet.
              </p>
            ) : null}
            <ul className="divide-y">
              {fulfillments.data?.map((fulfillment) => (
                <li className="py-3" key={fulfillment.id}>
                  <p className="font-medium">
                    {names.products.get(fulfillment.productId) ??
                      fulfillment.productId}{" "}
                    · {fulfillment.quantity}
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {names.warehouses.get(fulfillment.warehouseId) ??
                      fulfillment.warehouseId}
                  </p>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
