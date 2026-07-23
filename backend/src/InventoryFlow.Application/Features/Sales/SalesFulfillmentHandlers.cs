using MediatR;

namespace InventoryFlow.Application.Features.Sales;

public sealed class RecordSalesFulfillmentHandler(ISalesFulfillmentService service, TimeProvider timeProvider)
    : IRequestHandler<RecordSalesFulfillmentCommand, SalesFulfillmentResponse?>
{
    public async Task<SalesFulfillmentResponse?> Handle(RecordSalesFulfillmentCommand request, CancellationToken cancellationToken)
    {
        var fulfillment = await service.RecordAsync(request.WorkspaceId, request.WarehouseId, request.ProductId, request.Quantity,
            request.IdempotencyKey, timeProvider.GetUtcNow(), cancellationToken);
        return fulfillment is null ? null : SalesFulfillmentResponse.From(fulfillment);
    }
}

public sealed class ListSalesFulfillmentsHandler(ISalesFulfillmentService service)
    : IRequestHandler<ListSalesFulfillmentsQuery, IReadOnlyList<SalesFulfillmentResponse>>
{
    public async Task<IReadOnlyList<SalesFulfillmentResponse>> Handle(ListSalesFulfillmentsQuery request,
        CancellationToken cancellationToken) =>
        (await service.ListAsync(request.WorkspaceId, cancellationToken)).Select(SalesFulfillmentResponse.From).ToArray();
}
