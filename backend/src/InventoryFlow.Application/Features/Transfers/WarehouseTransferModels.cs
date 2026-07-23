using System.Text.Json.Serialization;
using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Features.Transfers;

public sealed record RecordWarehouseTransferRequest(Guid SourceWarehouseId, Guid DestinationWarehouseId, Guid ProductId,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal Quantity, string IdempotencyKey);

public sealed record RecordWarehouseTransferCommand(Guid WorkspaceId, Guid SourceWarehouseId, Guid DestinationWarehouseId,
    Guid ProductId, decimal Quantity, string IdempotencyKey) : MediatR.IRequest<WarehouseTransferResponse?>;

public sealed record ListWarehouseTransfersQuery(Guid WorkspaceId) : MediatR.IRequest<IReadOnlyList<WarehouseTransferResponse>>;

public sealed record WarehouseTransferResponse(Guid Id, Guid SourceWarehouseId, Guid DestinationWarehouseId, Guid ProductId,
    decimal Quantity, Guid SourceInventoryMovementId, Guid DestinationInventoryMovementId, DateTimeOffset TransferredAtUtc)
{
    public static WarehouseTransferResponse From(WarehouseTransfer transfer) => new(transfer.Id, transfer.SourceWarehouseId,
        transfer.DestinationWarehouseId, transfer.ProductId, transfer.Quantity, transfer.SourceInventoryMovementId,
        transfer.DestinationInventoryMovementId, transfer.TransferredAtUtc);
}

public interface IWarehouseTransferService
{
    Task<WarehouseTransfer?> RecordAsync(Guid workspaceId, Guid sourceWarehouseId, Guid destinationWarehouseId, Guid productId,
        decimal quantity, string idempotencyKey, DateTimeOffset transferredAtUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<WarehouseTransfer>> ListAsync(Guid workspaceId, CancellationToken cancellationToken);
}
