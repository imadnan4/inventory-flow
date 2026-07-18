using InventoryFlow.Domain.Entities;
using MediatR;

namespace InventoryFlow.Application.Features.Inventory;

/// <summary>Handles receipt recording.</summary>
public sealed class RecordReceiptHandler(IInventoryLedger ledger, TimeProvider timeProvider)
    : IRequestHandler<RecordReceiptCommand, InventoryMovementResponse?>
{
    /// <inheritdoc />
    public async Task<InventoryMovementResponse?> Handle(RecordReceiptCommand request, CancellationToken cancellationToken)
    {
        var movement = await ledger.RecordAsync(request.WorkspaceId, request.WarehouseId, request.ProductId,
            InventoryMovementType.Receipt, request.Quantity, request.IdempotencyKey, timeProvider.GetUtcNow(), cancellationToken);
        return movement is null ? null : InventoryMovementResponse.From(movement);
    }
}

/// <summary>Handles issue recording.</summary>
public sealed class RecordIssueHandler(IInventoryLedger ledger, TimeProvider timeProvider)
    : IRequestHandler<RecordIssueCommand, InventoryMovementResponse?>
{
    /// <inheritdoc />
    public async Task<InventoryMovementResponse?> Handle(RecordIssueCommand request, CancellationToken cancellationToken)
    {
        var movement = await ledger.RecordAsync(request.WorkspaceId, request.WarehouseId, request.ProductId,
            InventoryMovementType.Issue, request.Quantity, request.IdempotencyKey, timeProvider.GetUtcNow(), cancellationToken);
        return movement is null ? null : InventoryMovementResponse.From(movement);
    }
}

/// <summary>Handles current balance listing.</summary>
public sealed class ListInventoryBalancesHandler(IInventoryLedger ledger)
    : IRequestHandler<ListInventoryBalancesQuery, IReadOnlyList<InventoryBalanceResponse>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<InventoryBalanceResponse>> Handle(ListInventoryBalancesQuery request, CancellationToken cancellationToken) =>
        (await ledger.ListBalancesAsync(request.WorkspaceId, request.WarehouseId, request.ProductId, cancellationToken))
        .Select(InventoryBalanceResponse.From).ToArray();
}
