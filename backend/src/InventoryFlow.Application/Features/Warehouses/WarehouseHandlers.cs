using InventoryFlow.Domain.Entities;
using MediatR;

namespace InventoryFlow.Application.Features.Warehouses;

/// <summary>Handles warehouse creation.</summary>
public sealed class CreateWarehouseHandler(IWarehouseCatalog catalog, TimeProvider timeProvider)
    : IRequestHandler<CreateWarehouseCommand, WarehouseResponse>
{
    /// <inheritdoc />
    public async Task<WarehouseResponse> Handle(CreateWarehouseCommand request, CancellationToken cancellationToken) =>
        WarehouseResponse.From(await catalog.CreateAsync(new Warehouse(Guid.NewGuid(), request.WorkspaceId, request.Name,
            timeProvider.GetUtcNow()), cancellationToken));
}

/// <summary>Handles active warehouse listing.</summary>
public sealed class ListWarehousesHandler(IWarehouseCatalog catalog)
    : IRequestHandler<ListWarehousesQuery, IReadOnlyList<WarehouseResponse>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<WarehouseResponse>> Handle(ListWarehousesQuery request, CancellationToken cancellationToken) =>
        (await catalog.ListActiveAsync(request.WorkspaceId, cancellationToken)).Select(WarehouseResponse.From).ToArray();
}

/// <summary>Handles warehouse archival.</summary>
public sealed class ArchiveWarehouseHandler(IWarehouseCatalog catalog, TimeProvider timeProvider)
    : IRequestHandler<ArchiveWarehouseCommand, bool>
{
    /// <inheritdoc />
    public Task<bool> Handle(ArchiveWarehouseCommand request, CancellationToken cancellationToken) =>
        catalog.ArchiveAsync(request.WorkspaceId, request.WarehouseId, timeProvider.GetUtcNow(), cancellationToken);
}
