import { useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import { PageHeader } from "@/components/shared/PageHeader"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { useAuthStore } from "@/features/auth/auth-store"
import { listProducts } from "@/features/products/products-api"
import {
  listWarehouseTransfers,
  recordWarehouseTransfer,
} from "@/features/transfers/transfers-api"
import { inventoryBalancesKeyPrefix } from "@/features/inventory/inventory-queries"
import { warehouseTransferSchema } from "@/features/transfers/transfers-schema"
import type { RecordWarehouseTransferPayload } from "@/features/transfers/types"
import { listWarehouses } from "@/features/warehouses/warehouses-api"

const key = (name: string, userId: string, workspaceId: string) =>
  [name, userId, workspaceId] as const
const readError = (error: unknown) => {
  if (typeof error === "object" && error && "response" in error) {
    const data = (
      error as { response?: { data?: { detail?: string; title?: string } } }
    ).response?.data
    return data?.detail ?? data?.title ?? "Unable to transfer inventory."
  }
  return "Unable to transfer inventory."
}

export function Component() {
  const queryClient = useQueryClient()
  const user = useAuthStore((state) => state.user)
  const userId = user?.id ?? "anonymous"
  const workspaceId = user?.workspace.id ?? "none"
  const [sourceWarehouseId, setSourceWarehouseId] = useState("")
  const [destinationWarehouseId, setDestinationWarehouseId] = useState("")
  const [productId, setProductId] = useState("")
  const [quantity, setQuantity] = useState("")
  const [formError, setFormError] = useState("")
  const [retryTransfer, setRetryTransfer] =
    useState<RecordWarehouseTransferPayload | null>(null)
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
  const transfers = useQuery({
    queryKey: key("transfers", userId, workspaceId),
    queryFn: listWarehouseTransfers,
    enabled: user !== null,
  })
  const mutation = useMutation({
    mutationKey: key("transfers", userId, workspaceId),
    mutationFn: recordWarehouseTransfer,
    onSuccess: () => {
      setFormError("")
      setRetryTransfer(null)
      setQuantity("")
      queryClient.invalidateQueries({
        queryKey: key("transfers", userId, workspaceId),
      })
      queryClient.invalidateQueries({
        queryKey: inventoryBalancesKeyPrefix(userId, workspaceId),
      })
    },
    onError: (error, variables) => {
      setRetryTransfer(variables)
      setFormError(readError(error))
    },
  })
  const submit = () => {
    const parsed = warehouseTransferSchema.safeParse({
      sourceWarehouseId,
      destinationWarehouseId,
      productId,
      quantity,
    })
    if (!parsed.success) {
      setFormError(
        parsed.error.issues[0]?.message ?? "Check the transfer details."
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
        title="Transfers"
        description="Move one product between warehouses atomically."
      />
      <div className="grid gap-6 xl:grid-cols-[22rem_minmax(0,1fr)]">
        <Card>
          <CardHeader>
            <CardTitle>Transfer inventory</CardTitle>
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
                Source warehouse
                <select
                  className="mt-1 w-full rounded-md border bg-background p-2"
                  disabled={disabled}
                  onChange={(e) => setSourceWarehouseId(e.target.value)}
                  value={sourceWarehouseId}
                >
                  <option value="">Choose a source warehouse</option>
                  {warehouses.data?.map((x) => (
                    <option key={x.id} value={x.id}>
                      {x.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="block text-sm">
                Destination warehouse
                <select
                  className="mt-1 w-full rounded-md border bg-background p-2"
                  disabled={disabled}
                  onChange={(e) => setDestinationWarehouseId(e.target.value)}
                  value={destinationWarehouseId}
                >
                  <option value="">Choose a destination warehouse</option>
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
                  onChange={(e) => setProductId(e.target.value)}
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
                  onChange={(e) => setQuantity(e.target.value)}
                  placeholder="0.0000"
                  value={quantity}
                />
              </label>
              {formError ? (
                <p className="text-sm text-destructive" role="alert">
                  {formError}
                </p>
              ) : null}
              {retryTransfer ? (
                <Button
                  disabled={mutation.isPending}
                  onClick={() => mutation.mutate(retryTransfer)}
                  type="button"
                  variant="outline"
                >
                  Retry transfer
                </Button>
              ) : null}
              <Button disabled={disabled} onClick={submit} type="button">
                {mutation.isPending ? "Transferring…" : "Transfer inventory"}
              </Button>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Transfer history</CardTitle>
          </CardHeader>
          <CardContent>
            {transfers.isLoading ? (
              <p className="text-muted-foreground">Loading transfers…</p>
            ) : null}
            {transfers.isError ? (
              <p className="text-destructive" role="alert">
                {readError(transfers.error)}
              </p>
            ) : null}
            {transfers.data?.length === 0 ? (
              <p className="text-muted-foreground">No transfers yet.</p>
            ) : null}
            <ul className="divide-y">
              {transfers.data?.map((transfer) => (
                <li className="py-3" key={transfer.id}>
                  <p className="font-medium">
                    {names.products.get(transfer.productId) ??
                      transfer.productId}{" "}
                    · {transfer.quantity}
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {names.warehouses.get(transfer.sourceWarehouseId) ??
                      transfer.sourceWarehouseId}{" "}
                    →{" "}
                    {names.warehouses.get(transfer.destinationWarehouseId) ??
                      transfer.destinationWarehouseId}
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
