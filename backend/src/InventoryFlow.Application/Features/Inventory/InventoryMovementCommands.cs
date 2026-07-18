using System.Text.Json.Serialization;
using MediatR;
using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Features.Inventory;

/// <summary>Describes a request that records an immutable inventory movement.</summary>
public interface IInventoryMovementCommand
{
    /// <summary>Gets the workspace owning the movement.</summary>
    Guid WorkspaceId { get; }
    /// <summary>Gets the warehouse receiving or issuing stock.</summary>
    Guid WarehouseId { get; }
    /// <summary>Gets the product receiving or issuing stock.</summary>
    Guid ProductId { get; }
    /// <summary>Gets the positive movement quantity.</summary>
    decimal Quantity { get; }
    /// <summary>Gets the retry-stable client idempotency key.</summary>
    string IdempotencyKey { get; }
}

/// <summary>Supplies browser input for recording a movement.</summary>
public sealed record RecordInventoryMovementRequest(Guid WarehouseId, Guid ProductId,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal Quantity, string IdempotencyKey);

/// <summary>Records a stock receipt in a server-resolved workspace.</summary>
public sealed record RecordReceiptCommand(Guid WorkspaceId, Guid WarehouseId, Guid ProductId, decimal Quantity,
    string IdempotencyKey) : IRequest<InventoryMovementResponse?>, IInventoryMovementCommand;

/// <summary>Records a stock issue in a server-resolved workspace.</summary>
public sealed record RecordIssueCommand(Guid WorkspaceId, Guid WarehouseId, Guid ProductId, decimal Quantity,
    string IdempotencyKey) : IRequest<InventoryMovementResponse?>, IInventoryMovementCommand;

