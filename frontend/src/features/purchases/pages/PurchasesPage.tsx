import { useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import { PageHeader } from "@/components/shared/PageHeader"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { useAuthStore } from "@/features/auth/auth-store"
import { listProducts } from "@/features/products/products-api"
import {
  listPurchaseReceipts,
  recordPurchaseReceipt,
} from "@/features/purchases/purchases-api"
import { inventoryBalancesKeyPrefix } from "@/features/inventory/inventory-queries"
import { purchaseReceiptSchema } from "@/features/purchases/purchases-schema"
import type { RecordPurchaseReceiptPayload } from "@/features/purchases/types"
import { listSuppliers } from "@/features/suppliers/suppliers-api"
import { listWarehouses } from "@/features/warehouses/warehouses-api"

const key = (name: string, userId: string, workspaceId: string) =>
  [name, userId, workspaceId] as const
const readError = (error: unknown) => {
  if (typeof error === "object" && error && "response" in error) {
    const data = (
      error as { response?: { data?: { detail?: string; title?: string } } }
    ).response?.data
    return data?.detail ?? data?.title ?? "Unable to receive the purchase."
  }
  return "Unable to receive the purchase."
}

export function Component() {
  const queryClient = useQueryClient()
  const user = useAuthStore((state) => state.user)
  const userId = user?.id ?? "anonymous"
  const workspaceId = user?.workspace.id ?? "none"
  const [supplierId, setSupplierId] = useState("")
  const [warehouseId, setWarehouseId] = useState("")
  const [productId, setProductId] = useState("")
  const [quantity, setQuantity] = useState("")
  const [formError, setFormError] = useState("")
  const [retryReceipt, setRetryReceipt] =
    useState<RecordPurchaseReceiptPayload | null>(null)

  const clearRetry = () => { if (retryReceipt) setRetryReceipt(null) }
  const suppliers = useQuery({
    queryKey: key("suppliers", userId, workspaceId),
    queryFn: listSuppliers,
    enabled: user !== null,
  })
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
  const receipts = useQuery({
    queryKey: key("purchases-receipts", userId, workspaceId),
    queryFn: listPurchaseReceipts,
    enabled: user !== null,
  })
  const mutation = useMutation({
    mutationKey: key("purchases-receipts", userId, workspaceId),
    mutationFn: recordPurchaseReceipt,
    onSuccess: () => {
      setFormError("")
      setRetryReceipt(null)
      setQuantity("")
      queryClient.invalidateQueries({
        queryKey: key("purchases-receipts", userId, workspaceId),
      })
      queryClient.invalidateQueries({
        queryKey: inventoryBalancesKeyPrefix(userId, workspaceId),
      })
    },
    onError: (error, variables) => {
      setRetryReceipt(variables)
      setFormError(readError(error))
    },
  })
  const submit = () => {
    const parsed = purchaseReceiptSchema.safeParse({
      supplierId,
      warehouseId,
      productId,
      quantity,
    })
    if (!parsed.success) {
      setFormError(
        parsed.error.issues[0]?.message ?? "Check the receipt details."
      )
      return
    }
    setFormError("")
    mutation.mutate({ ...parsed.data, idempotencyKey: crypto.randomUUID() })
  }
  const loading =
    suppliers.isLoading || warehouses.isLoading || products.isLoading
  const names = {
    suppliers: new Map(suppliers.data?.map((x) => [x.id, x.name])),
    warehouses: new Map(warehouses.data?.map((x) => [x.id, x.name])),
    products: new Map(products.data?.map((x) => [x.id, x.name])),
  }
  const disabled = loading || mutation.isPending
  return (
    <div className="space-y-6">
      <PageHeader
        title="Purchases"
        description="Receive single-line supplier goods receipts into inventory."
      />
      <div className="grid gap-6 xl:grid-cols-[22rem_minmax(0,1fr)]">
        <Card>
          <CardHeader>
            <CardTitle>Receive purchase</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-4">
              {loading ? (
                <p className="text-muted-foreground">Loading catalog…</p>
              ) : null}
              {suppliers.isError || warehouses.isError || products.isError ? (
                <p className="text-destructive" role="alert">
                  {readError(
                    suppliers.error ?? warehouses.error ?? products.error
                  )}
                </p>
              ) : null}
              <label className="block text-sm">
                Supplier
                <select
                  className="mt-1 w-full rounded-md border bg-background p-2"
                  disabled={disabled}
                  onChange={(e) => { setSupplierId(e.target.value); clearRetry() }}
                  value={supplierId}
                >
                  <option value="">Choose a supplier</option>
                  {suppliers.data?.map((x) => (
                    <option key={x.id} value={x.id}>
                      {x.name}
                    </option>
                  ))}
                </select>
              </label>
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
              {retryReceipt ? (
                <Button
                  disabled={mutation.isPending}
                  onClick={() => mutation.mutate(retryReceipt)}
                  type="button"
                  variant="outline"
                >
                  Retry receipt
                </Button>
              ) : null}
              <Button disabled={disabled} onClick={submit} type="button">
                {mutation.isPending ? "Receiving…" : "Receive purchase"}
              </Button>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Receipt history</CardTitle>
          </CardHeader>
          <CardContent>
            {receipts.isLoading ? (
              <p className="text-muted-foreground">Loading receipts…</p>
            ) : null}
            {receipts.isError ? (
              <p className="text-destructive" role="alert">
                {readError(receipts.error)}
              </p>
            ) : null}
            {receipts.data?.length === 0 ? (
              <p className="text-muted-foreground">No purchase receipts yet.</p>
            ) : null}
            <ul className="divide-y">
              {receipts.data?.map((receipt) => (
                <li className="py-3" key={receipt.id}>
                  <p className="font-medium">
                    {names.products.get(receipt.productId) ?? receipt.productId}{" "}
                    · {receipt.quantity}
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {names.suppliers.get(receipt.supplierId) ??
                      receipt.supplierId}{" "}
                    →{" "}
                    {names.warehouses.get(receipt.warehouseId) ??
                      receipt.warehouseId}
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
