export const inventoryBalancesKey = (
  userId: string,
  workspaceId: string,
  warehouseId = "",
  productId = ""
) =>
  [
    "inventory",
    "balances",
    userId,
    workspaceId,
    warehouseId,
    productId,
  ] as const

export const inventoryBalancesKeyPrefix = (
  userId: string,
  workspaceId: string
) => ["inventory", "balances", userId, workspaceId] as const
