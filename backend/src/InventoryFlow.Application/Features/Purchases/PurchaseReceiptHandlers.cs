using MediatR;

namespace InventoryFlow.Application.Features.Purchases;

public sealed class RecordPurchaseReceiptHandler(IPurchaseReceiptService service, TimeProvider timeProvider)
    : IRequestHandler<RecordPurchaseReceiptCommand, PurchaseReceiptResponse?>
{
    public async Task<PurchaseReceiptResponse?> Handle(RecordPurchaseReceiptCommand request, CancellationToken cancellationToken)
    {
        var receipt = await service.RecordAsync(request.WorkspaceId, request.SupplierId, request.WarehouseId, request.ProductId,
            request.Quantity, request.IdempotencyKey, timeProvider.GetUtcNow(), cancellationToken);
        return receipt is null ? null : PurchaseReceiptResponse.From(receipt);
    }
}

public sealed class ListPurchaseReceiptsHandler(IPurchaseReceiptService service)
    : IRequestHandler<ListPurchaseReceiptsQuery, IReadOnlyList<PurchaseReceiptResponse>>
{
    public async Task<IReadOnlyList<PurchaseReceiptResponse>> Handle(ListPurchaseReceiptsQuery request, CancellationToken cancellationToken) =>
        (await service.ListAsync(request.WorkspaceId, cancellationToken)).Select(PurchaseReceiptResponse.From).ToArray();
}
