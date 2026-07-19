import { useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import { useAuthStore } from "@/features/auth/auth-store"
import {
  listInventoryBalances,
  recordInventoryMovement,
} from "@/features/inventory/inventory-api"
import { inventoryMovementSchema } from "@/features/inventory/inventory-schema"
import {
  inventoryBalancesKey,
  inventoryBalancesKeyPrefix,
} from "@/features/inventory/inventory-queries"
import type { RecordInventoryMovementPayload } from "@/features/inventory/types"
import { listProducts } from "@/features/products/products-api"
import { listWarehouses } from "@/features/warehouses/warehouses-api"
import { PageHeader } from "@/components/shared/PageHeader"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"

const productsKey = (userId: string, workspaceId: string) =>
  ["products", userId, workspaceId] as const
const warehousesKey = (userId: string, workspaceId: string) =>
  ["warehouses", userId, workspaceId] as const
const inventoryMovementKey = (userId: string, workspaceId: string) =>
  ["inventory", "movement", userId, workspaceId] as const

type MovementInput = RecordInventoryMovementPayload & {
  kind: "receipt" | "issue"
}

const readError = (error: unknown) => {
  if (typeof error === "object" && error && "response" in error) {
    const data = (
      error as { response?: { data?: { detail?: string; title?: string } } }
    ).response?.data
    return data?.detail ?? data?.title ?? "Unable to record the movement."
  }
  return "Unable to record the movement."
}

const displayQuantity = (quantity: number) =>
  new Intl.NumberFormat(undefined, { maximumFractionDigits: 4 }).format(
    quantity
  )

export function Component() {
  const queryClient = useQueryClient()
  const user = useAuthStore((state) => state.user)
  const userId = user?.id ?? "anonymous"
  const workspaceId = user?.workspace.id ?? "none"
  const [warehouseId, setWarehouseId] = useState("")
  const [productId, setProductId] = useState("")
  const [quantity, setQuantity] = useState("")
  const [formError, setFormError] = useState("")
  const [retryMovement, setRetryMovement] = useState<MovementInput | null>(null)

  const products = useQuery({
    queryKey: productsKey(userId, workspaceId),
    queryFn: listProducts,
    enabled: user !== null,
  })
  const warehouses = useQuery({
    queryKey: warehousesKey(userId, workspaceId),
    queryFn: listWarehouses,
    enabled: user !== null,
  })
  const balances = useQuery({
    queryKey: inventoryBalancesKey(userId, workspaceId, warehouseId, productId),
    queryFn: () =>
      listInventoryBalances({
        warehouseId: warehouseId || undefined,
        productId: productId || undefined,
      }),
    enabled: user !== null,
  })
  const movement = useMutation({
    mutationKey: inventoryMovementKey(userId, workspaceId),
    mutationFn: ({ kind, ...payload }: MovementInput) =>
      recordInventoryMovement(kind, payload),
    onSuccess: () => {
      setFormError("")
      setRetryMovement(null)
      setQuantity("")
      queryClient.invalidateQueries({
        queryKey: inventoryBalancesKeyPrefix(userId, workspaceId),
      })
    },
    onError: (reason, variables) => {
      setRetryMovement(variables)
      setFormError(readError(reason))
    },
  })

  const submit = (kind: "receipt" | "issue") => {
    const parsed = inventoryMovementSchema.safeParse({
      warehouseId,
      productId,
      quantity,
    })
    if (!parsed.success) {
      setFormError(
        parsed.error.issues[0]?.message ?? "Check the movement details."
      )
      return
    }

    setFormError("")
    movement.mutate({
      ...parsed.data,
      kind,
      idempotencyKey: crypto.randomUUID(),
    })
  }

  const productNames = new Map(
    products.data?.map((product) => [product.id, product.name])
  )
  const warehouseNames = new Map(
    warehouses.data?.map((warehouse) => [warehouse.id, warehouse.name])
  )
  const catalogLoading = products.isLoading || warehouses.isLoading
  const canRecord = !catalogLoading && !movement.isPending

  return (
    <div className="space-y-6">
      <PageHeader
        description="Receive stock and issue on-hand inventory in your workspace."
        title="Inventory"
      />
      <div className="grid gap-6 xl:grid-cols-[22rem_minmax(0,1fr)]">
        <Card>
          <CardHeader>
            <CardTitle>Record movement</CardTitle>
          </CardHeader>
          <CardContent>
            {catalogLoading ? (
              <p className="text-muted-foreground">
                Loading products and warehouses…
              </p>
            ) : null}
            {products.isError || warehouses.isError ? (
              <p className="text-destructive" role="alert">
                {readError(products.error ?? warehouses.error)}
              </p>
            ) : null}
            <div className="space-y-4">
              <label className="block text-sm">
                Warehouse
                <select
                  className="mt-1 w-full rounded-md border bg-background p-2"
                  disabled={catalogLoading || movement.isPending}
                  onChange={(event) => setWarehouseId(event.target.value)}
                  value={warehouseId}
                >
                  <option value="">Choose a warehouse</option>
                  {warehouses.data?.map((warehouse) => (
                    <option key={warehouse.id} value={warehouse.id}>
                      {warehouse.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="block text-sm">
                Product
                <select
                  className="mt-1 w-full rounded-md border bg-background p-2"
                  disabled={catalogLoading || movement.isPending}
                  onChange={(event) => setProductId(event.target.value)}
                  value={productId}
                >
                  <option value="">Choose a product</option>
                  {products.data?.map((product) => (
                    <option key={product.id} value={product.id}>
                      {product.name} ({product.sku})
                    </option>
                  ))}
                </select>
              </label>
              <label className="block text-sm">
                Quantity
                <input
                  className="mt-1 w-full rounded-md border bg-background p-2"
                  disabled={movement.isPending}
                  inputMode="decimal"
                  onChange={(event) => setQuantity(event.target.value)}
                  placeholder="0.0000"
                  value={quantity}
                />
              </label>
              {formError ? (
                <p className="text-sm text-destructive" role="alert">
                  {formError}
                </p>
              ) : null}
              {retryMovement ? (
                <Button
                  disabled={movement.isPending}
                  onClick={() => movement.mutate(retryMovement)}
                  type="button"
                  variant="outline"
                >
                  Retry {retryMovement.kind}
                </Button>
              ) : null}
              <div className="flex gap-3">
                <Button
                  disabled={!canRecord}
                  onClick={() => submit("receipt")}
                  type="button"
                >
                  {movement.isPending ? "Recording…" : "Receipt"}
                </Button>
                <Button
                  disabled={!canRecord}
                  onClick={() => submit("issue")}
                  type="button"
                  variant="outline"
                >
                  Issue
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>On hand</CardTitle>
          </CardHeader>
          <CardContent>
            {balances.isLoading ? (
              <p className="text-muted-foreground">Loading balances…</p>
            ) : null}
            {balances.isError ? (
              <p className="text-destructive" role="alert">
                {readError(balances.error)}
              </p>
            ) : null}
            {balances.data?.length === 0 ? (
              <p className="text-muted-foreground">
                No inventory balances match these selections.
              </p>
            ) : null}
            {balances.data?.length ? (
              <ul className="divide-y">
                {balances.data.map((balance) => (
                  <li
                    className="flex items-center justify-between gap-4 py-3"
                    key={`${balance.warehouseId}-${balance.productId}`}
                  >
                    <div>
                      <p className="font-medium">
                        {productNames.get(balance.productId) ??
                          balance.productId}
                      </p>
                      <p className="text-sm text-muted-foreground">
                        {warehouseNames.get(balance.warehouseId) ??
                          balance.warehouseId}
                      </p>
                    </div>
                    <span className="font-medium">
                      {displayQuantity(balance.quantity)}
                    </span>
                  </li>
                ))}
              </ul>
            ) : null}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
