using MediatR;

namespace InventoryFlow.Application.Features.Inventory;

/// <summary>Lists current workspace inventory balances.</summary>
public sealed record ListInventoryBalancesQuery(Guid WorkspaceId, Guid? WarehouseId, Guid? ProductId)
    : IRequest<IReadOnlyList<InventoryBalanceResponse>>;
