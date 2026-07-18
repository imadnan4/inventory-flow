import { useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import { PageHeader } from "@/components/shared/PageHeader"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import {
  archiveWarehouse,
  createWarehouse,
  listWarehouses,
} from "@/features/warehouses/warehouses-api"
import { warehouseSchema } from "@/features/warehouses/warehouses-schema"
import { useAuthStore } from "@/features/auth/auth-store"

const warehousesKey = (userId: string, workspaceId: string) =>
  ["warehouses", userId, workspaceId] as const

const readError = (error: unknown) => {
  if (typeof error === "object" && error && "response" in error) {
    const data = (
      error as { response?: { data?: { detail?: string; title?: string } } }
    ).response?.data
    return data?.detail ?? data?.title ?? "Unable to save the warehouse."
  }
  return "Unable to save the warehouse."
}

export function Component() {
  const queryClient = useQueryClient()
  const user = useAuthStore((state) => state.user)
  const [error, setError] = useState("")
  const queryKey = warehousesKey(
    user?.id ?? "anonymous",
    user?.workspace.id ?? "none"
  )
  const warehouses = useQuery({
    queryKey,
    queryFn: listWarehouses,
    enabled: user !== null,
  })
  const create = useMutation({
    mutationFn: createWarehouse,
    onSuccess: () => {
      setError("")
      queryClient.invalidateQueries({ queryKey })
    },
    onError: (reason) => setError(readError(reason)),
  })
  const archive = useMutation({
    mutationFn: archiveWarehouse,
    onSuccess: () => queryClient.invalidateQueries({ queryKey }),
    onError: (reason) => setError(readError(reason)),
  })

  const submit = (form: FormData) => {
    const parsed = warehouseSchema.safeParse(Object.fromEntries(form))
    if (!parsed.success)
      return setError(
        parsed.error.issues[0]?.message ?? "Check the warehouse details."
      )
    setError("")
    create.mutate(parsed.data)
  }

  return (
    <div className="space-y-6">
      <PageHeader
        description="Manage the active warehouses in your workspace."
        title="Warehouses"
      />
      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_22rem]">
        <Card>
          <CardHeader>
            <CardTitle>Catalog</CardTitle>
          </CardHeader>
          <CardContent>
            {warehouses.isLoading ? (
              <p className="text-muted-foreground">Loading warehouses…</p>
            ) : null}
            {warehouses.isError ? (
              <p className="text-destructive" role="alert">
                {readError(warehouses.error)}
              </p>
            ) : null}
            {warehouses.data?.length === 0 ? (
              <p className="text-muted-foreground">No active warehouses yet.</p>
            ) : null}
            {warehouses.data?.length ? (
              <ul className="divide-y">
                {warehouses.data.map((warehouse) => (
                  <li
                    className="flex items-center justify-between gap-4 py-3"
                    key={warehouse.id}
                  >
                    <div>
                      <p className="font-medium">{warehouse.name}</p>
                      <p className="text-sm text-muted-foreground">
                        {warehouse.name}
                      </p>
                    </div>
                    <Button
                      disabled={archive.isPending}
                      onClick={() => archive.mutate(warehouse.id)}
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
            <CardTitle>Add warehouse</CardTitle>
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
                name
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
                {create.isPending ? "Adding…" : "Add warehouse"}
              </Button>
            </form>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
