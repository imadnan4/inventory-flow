import { useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import { PageHeader } from "@/components/shared/PageHeader"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import {
  archiveProduct,
  createProduct,
  listProducts,
} from "@/features/products/products-api"
import { productSchema } from "@/features/products/products-schema"
import { useAuthStore } from "@/features/auth/auth-store"

const productsKey = (userId: string, workspaceId: string) =>
  ["products", userId, workspaceId] as const

const readError = (error: unknown) => {
  if (typeof error === "object" && error && "response" in error) {
    const data = (
      error as { response?: { data?: { detail?: string; title?: string } } }
    ).response?.data
    return data?.detail ?? data?.title ?? "Unable to save the product."
  }
  return "Unable to save the product."
}

export function Component() {
  const queryClient = useQueryClient()
  const user = useAuthStore((state) => state.user)
  const [error, setError] = useState("")
  const queryKey = productsKey(
    user?.id ?? "anonymous",
    user?.workspace.id ?? "none"
  )
  const products = useQuery({
    queryKey,
    queryFn: listProducts,
    enabled: user !== null,
  })
  const create = useMutation({
    mutationFn: createProduct,
    onSuccess: () => {
      setError("")
      queryClient.invalidateQueries({ queryKey })
    },
    onError: (reason) => setError(readError(reason)),
  })
  const archive = useMutation({
    mutationFn: archiveProduct,
    onSuccess: () => queryClient.invalidateQueries({ queryKey }),
    onError: (reason) => setError(readError(reason)),
  })

  const submit = async (form: FormData) => {
    const parsed = productSchema.safeParse(Object.fromEntries(form))
    if (!parsed.success)
      return setError(
        parsed.error.issues[0]?.message ?? "Check the product details."
      )
    setError("")
    await create.mutateAsync(parsed.data).catch(() => {})
  }

  return (
    <div className="space-y-6">
      <PageHeader
        description="Manage the active products in your workspace."
        title="Products"
      />
      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_22rem]">
        <Card>
          <CardHeader>
            <CardTitle>Catalog</CardTitle>
          </CardHeader>
          <CardContent>
            {products.isLoading ? (
              <p className="text-muted-foreground">Loading products…</p>
            ) : null}
            {products.isError ? (
              <p className="text-destructive" role="alert">
                {readError(products.error)}
              </p>
            ) : null}
            {products.data?.length === 0 ? (
              <p className="text-muted-foreground">No active products yet.</p>
            ) : null}
            {products.data?.length ? (
              <ul className="divide-y">
                {products.data.map((product) => (
                  <li
                    className="flex items-center justify-between gap-4 py-3"
                    key={product.id}
                  >
                    <div>
                      <p className="font-medium">{product.name}</p>
                      <p className="text-sm text-muted-foreground">
                        {product.sku}
                      </p>
                    </div>
                    <Button
                      disabled={archive.isPending}
                      onClick={() => archive.mutate(product.id)}
                      size="sm"
                      variant="outline"
                    >
                      Archive
                    </Button>
                  </li>
                ))}
              </ul>
            ) : null}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Add product</CardTitle>
          </CardHeader>
          <CardContent>
            <form action={submit} className="space-y-4">
              <label className="block text-sm">
                Name
                <input
                  className="mt-1 w-full rounded-md border bg-background p-2"
                  name="name"
                  required
                />
              </label>
              <label className="block text-sm">
                SKU
                <input
                  className="mt-1 w-full rounded-md border bg-background p-2"
                  name="sku"
                  required
                />
              </label>
              {error ? (
                <p className="text-sm text-destructive" role="alert">
                  {error}
                </p>
              ) : null}
              <Button disabled={create.isPending} type="submit">
                {create.isPending ? "Adding…" : "Add product"}
              </Button>
            </form>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
