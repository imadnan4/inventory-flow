import { useState } from "react"
import { useQuery } from "@tanstack/react-query"

import { PageHeader } from "@/components/shared/PageHeader"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { useAuthStore } from "@/features/auth/auth-store"
import { listInventoryBalances } from "@/features/inventory/inventory-api"
import { inventoryBalancesKey } from "@/features/inventory/inventory-queries"
import { listProducts } from "@/features/products/products-api"
import { listWarehouses } from "@/features/warehouses/warehouses-api"

const catalogKey = (name: string, userId: string, workspaceId: string) =>
  [name, userId, workspaceId] as const

const readError = (error: unknown) => {
  if (typeof error === "object" && error && "response" in error) {
    const data = (
      error as { response?: { data?: { detail?: string; title?: string } } }
    ).response?.data
    return (
      data?.detail ?? data?.title ?? "Unable to load inventory availability."
    )
  }
  return "Unable to load inventory availability."
}

const displayQuantity = (quantity: number) =>
  new Intl.NumberFormat(undefined, { maximumFractionDigits: 4 }).format(
    quantity
  )

export function Component() {
  const user = useAuthStore((state) => state.user)
  const userId = user?.id ?? "anonymous"
  const workspaceId = user?.workspace.id ?? "none"
  const [warehouseId, setWarehouseId] = useState("")
  const [productId, setProductId] = useState("")

  const products = useQuery({
    queryKey: catalogKey("products", userId, workspaceId),
    queryFn: listProducts,
    enabled: user !== null,
  })
  const warehouses = useQuery({
    queryKey: catalogKey("warehouses", userId, workspaceId),
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

  const productsById = new Map(
    products.data?.map((product) => [product.id, product])
  )
  const warehousesById = new Map(
    warehouses.data?.map((warehouse) => [warehouse.id, warehouse])
  )
  const catalogLoading = products.isLoading || warehouses.isLoading
  const catalogError = products.error ?? warehouses.error

  return (
    <div className="space-y-6">
      <PageHeader
        description="View current on-hand inventory by warehouse and product."
        title="Reports"
      />
      <Card>
        <CardHeader>
          <CardTitle>Inventory availability</CardTitle>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="grid gap-4 sm:grid-cols-2">
            <label className="block text-sm">
              Warehouse
              <select
                className="mt-1 w-full rounded-md border bg-background p-2"
                disabled={catalogLoading}
                onChange={(event) => setWarehouseId(event.target.value)}
                value={warehouseId}
              >
                <option value="">All warehouses</option>
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
                disabled={catalogLoading}
                onChange={(event) => setProductId(event.target.value)}
                value={productId}
              >
                <option value="">All products</option>
                {products.data?.map((product) => (
                  <option key={product.id} value={product.id}>
                    {product.name} ({product.sku})
                  </option>
                ))}
              </select>
            </label>
          </div>

          {catalogLoading ? (
            <p className="text-muted-foreground" role="status">
              Loading catalog…
            </p>
          ) : null}
          {catalogError ? (
            <p className="text-destructive" role="alert">
              {readError(catalogError)}
            </p>
          ) : null}
          {balances.isLoading ? (
            <p className="text-muted-foreground" role="status">
              Loading availability…
            </p>
          ) : null}
          {balances.isError ? (
            <p className="text-destructive" role="alert">
              {readError(balances.error)}
            </p>
          ) : null}
          {balances.data?.length === 0 ? (
            <p className="text-muted-foreground" role="status">
              No inventory balances match these filters.
            </p>
          ) : null}
          {balances.data?.length ? (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[36rem] text-left text-sm">
                <thead className="border-b text-muted-foreground">
                  <tr>
                    <th className="px-3 py-2 font-medium" scope="col">
                      Product
                    </th>
                    <th className="px-3 py-2 font-medium" scope="col">
                      SKU
                    </th>
                    <th className="px-3 py-2 font-medium" scope="col">
                      Warehouse
                    </th>
                    <th
                      className="px-3 py-2 text-right font-medium"
                      scope="col"
                    >
                      On hand
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {balances.data.map((balance) => {
                    const product = productsById.get(balance.productId)
                    const warehouse = warehousesById.get(balance.warehouseId)

                    return (
                      <tr key={`${balance.warehouseId}-${balance.productId}`}>
                        <td className="px-3 py-3 font-medium">
                          {product?.name ?? balance.productId}
                        </td>
                        <td className="px-3 py-3 text-muted-foreground">
                          {product?.sku ?? balance.productId}
                        </td>
                        <td className="px-3 py-3">
                          {warehouse?.name ?? balance.warehouseId}
                        </td>
                        <td className="px-3 py-3 text-right font-medium">
                          {displayQuantity(balance.quantity)}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          ) : null}
        </CardContent>
      </Card>
    </div>
  )
}
