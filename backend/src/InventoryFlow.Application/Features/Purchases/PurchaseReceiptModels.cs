using System.Text.Json.Serialization;
using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Features.Purchases;

public sealed record RecordPurchaseReceiptRequest(Guid SupplierId, Guid WarehouseId, Guid ProductId,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal Quantity, string IdempotencyKey);

public sealed record RecordPurchaseReceiptCommand(Guid WorkspaceId, Guid SupplierId, Guid WarehouseId, Guid ProductId,
    decimal Quantity, string IdempotencyKey) : MediatR.IRequest<PurchaseReceiptResponse?>;

public sealed record ListPurchaseReceiptsQuery(Guid WorkspaceId) : MediatR.IRequest<IReadOnlyList<PurchaseReceiptResponse>>;

public sealed record PurchaseReceiptResponse(Guid Id, Guid SupplierId, Guid WarehouseId, Guid ProductId, decimal Quantity,
    Guid InventoryMovementId, DateTimeOffset ReceivedAtUtc)
{
    public static PurchaseReceiptResponse From(PurchaseReceipt receipt) => new(receipt.Id, receipt.SupplierId, receipt.WarehouseId,
        receipt.ProductId, receipt.Quantity, receipt.InventoryMovementId, receipt.ReceivedAtUtc);
}

public interface IPurchaseReceiptService
{
    Task<PurchaseReceipt?> RecordAsync(Guid workspaceId, Guid supplierId, Guid warehouseId, Guid productId, decimal quantity,
        string idempotencyKey, DateTimeOffset receivedAtUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<PurchaseReceipt>> ListAsync(Guid workspaceId, CancellationToken cancellationToken);
}
