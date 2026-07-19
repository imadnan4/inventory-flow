import { useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import { PageHeader } from "@/components/shared/PageHeader"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import {
  archiveSupplier,
  createSupplier,
  listSuppliers,
} from "@/features/suppliers/suppliers-api"
import { supplierSchema } from "@/features/suppliers/suppliers-schema"
import { useAuthStore } from "@/features/auth/auth-store"

const suppliersKey = (userId: string, workspaceId: string) =>
  ["suppliers", userId, workspaceId] as const

const readError = (error: unknown) => {
  if (typeof error === "object" && error && "response" in error) {
    const data = (
      error as { response?: { data?: { detail?: string; title?: string } } }
    ).response?.data
    return data?.detail ?? data?.title ?? "Unable to save the supplier."
  }
  return "Unable to save the supplier."
}

export function Component() {
  const queryClient = useQueryClient()
  const user = useAuthStore((state) => state.user)
  const [error, setError] = useState("")
  const queryKey = suppliersKey(
    user?.id ?? "anonymous",
    user?.workspace.id ?? "none"
  )
  const suppliers = useQuery({
    queryKey,
    queryFn: listSuppliers,
    enabled: user !== null,
  })
  const create = useMutation({
    mutationFn: createSupplier,
    onSuccess: () => {
      setError("")
      queryClient.invalidateQueries({ queryKey })
    },
    onError: (reason) => setError(readError(reason)),
  })
  const archive = useMutation({
    mutationFn: archiveSupplier,
    onSuccess: () => queryClient.invalidateQueries({ queryKey }),
    onError: (reason) => setError(readError(reason)),
  })

  const submit = (form: FormData) => {
    const parsed = supplierSchema.safeParse(Object.fromEntries(form))
    if (!parsed.success)
      return setError(
        parsed.error.issues[0]?.message ?? "Check the supplier details."
      )
    setError("")
    create.mutate(parsed.data)
  }

  return (
    <div className="space-y-6">
      <PageHeader
        description="Manage the active suppliers in your workspace."
        title="Suppliers"
      />
      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_22rem]">
        <Card>
          <CardHeader>
            <CardTitle>Catalog</CardTitle>
          </CardHeader>
          <CardContent>
            {suppliers.isLoading ? (
              <p className="text-muted-foreground">Loading suppliers…</p>
            ) : null}
            {suppliers.isError ? (
              <p className="text-destructive" role="alert">
                {readError(suppliers.error)}
              </p>
            ) : null}
            {suppliers.data?.length === 0 ? (
              <p className="text-muted-foreground">No active suppliers yet.</p>
            ) : null}
            {suppliers.data?.length ? (
              <ul className="divide-y">
                {suppliers.data.map((supplier) => (
                  <li
                    className="flex items-center justify-between gap-4 py-3"
                    key={supplier.id}
                  >
                    <p className="font-medium">{supplier.name}</p>
                    <Button
                      disabled={archive.isPending}
                      onClick={() => archive.mutate(supplier.id)}
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
            <CardTitle>Add supplier</CardTitle>
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
              {error ? (
                <p className="text-sm text-destructive" role="alert">
                  {error}
                </p>
              ) : null}
              <Button disabled={create.isPending} type="submit">
                {create.isPending ? "Adding…" : "Add supplier"}
              </Button>
            </form>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
