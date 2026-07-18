using System.Text.Json.Serialization;
using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Features.Sales;

public sealed record RecordSalesFulfillmentRequest(Guid WarehouseId, Guid ProductId,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal Quantity, string IdempotencyKey);

public sealed record RecordSalesFulfillmentCommand(Guid WorkspaceId, Guid WarehouseId, Guid ProductId, decimal Quantity,
    string IdempotencyKey) : MediatR.IRequest<SalesFulfillmentResponse?>;

public sealed record ListSalesFulfillmentsQuery(Guid WorkspaceId) : MediatR.IRequest<IReadOnlyList<SalesFulfillmentResponse>>;

public sealed record SalesFulfillmentResponse(Guid Id, Guid WarehouseId, Guid ProductId, decimal Quantity,
    Guid InventoryMovementId, DateTimeOffset FulfilledAtUtc)
{
    public static SalesFulfillmentResponse From(SalesFulfillment fulfillment) => new(fulfillment.Id, fulfillment.WarehouseId,
        fulfillment.ProductId, fulfillment.Quantity, fulfillment.InventoryMovementId, fulfillment.FulfilledAtUtc);
}

public interface ISalesFulfillmentService
{
    Task<SalesFulfillment?> RecordAsync(Guid workspaceId, Guid warehouseId, Guid productId, decimal quantity, string idempotencyKey,
        DateTimeOffset fulfilledAtUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<SalesFulfillment>> ListAsync(Guid workspaceId, CancellationToken cancellationToken);
}
