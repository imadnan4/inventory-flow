using MediatR;

namespace InventoryFlow.Application.Features.Transfers;

public sealed class RecordWarehouseTransferHandler(IWarehouseTransferService service, TimeProvider timeProvider)
    : IRequestHandler<RecordWarehouseTransferCommand, WarehouseTransferResponse?>
{
    public async Task<WarehouseTransferResponse?> Handle(RecordWarehouseTransferCommand request, CancellationToken cancellationToken)
    {
        var transfer = await service.RecordAsync(request.WorkspaceId, request.SourceWarehouseId, request.DestinationWarehouseId,
            request.ProductId, request.Quantity, request.IdempotencyKey, timeProvider.GetUtcNow(), cancellationToken);
        return transfer is null ? null : WarehouseTransferResponse.From(transfer);
    }
}

public sealed class ListWarehouseTransfersHandler(IWarehouseTransferService service)
    : IRequestHandler<ListWarehouseTransfersQuery, IReadOnlyList<WarehouseTransferResponse>>
{
    public async Task<IReadOnlyList<WarehouseTransferResponse>> Handle(ListWarehouseTransfersQuery request,
        CancellationToken cancellationToken) =>
        (await service.ListAsync(request.WorkspaceId, cancellationToken)).Select(WarehouseTransferResponse.From).ToArray();
}
