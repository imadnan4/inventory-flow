using InventoryFlow.Domain.Entities;
using MediatR;

namespace InventoryFlow.Application.Features.Suppliers;

/// <summary>Handles supplier creation.</summary>
public sealed class CreateSupplierHandler(ISupplierCatalog catalog, TimeProvider timeProvider)
    : IRequestHandler<CreateSupplierCommand, SupplierResponse>
{
    /// <inheritdoc />
    public async Task<SupplierResponse> Handle(CreateSupplierCommand request, CancellationToken cancellationToken) =>
        SupplierResponse.From(await catalog.CreateAsync(new Supplier(Guid.NewGuid(), request.WorkspaceId, request.Name,
            timeProvider.GetUtcNow()), cancellationToken));
}

/// <summary>Handles active supplier listing.</summary>
public sealed class ListSuppliersHandler(ISupplierCatalog catalog)
    : IRequestHandler<ListSuppliersQuery, IReadOnlyList<SupplierResponse>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SupplierResponse>> Handle(ListSuppliersQuery request, CancellationToken cancellationToken) =>
        (await catalog.ListActiveAsync(request.WorkspaceId, cancellationToken)).Select(SupplierResponse.From).ToArray();
}

/// <summary>Handles idempotent supplier archival.</summary>
public sealed class ArchiveSupplierHandler(ISupplierCatalog catalog, TimeProvider timeProvider)
    : IRequestHandler<ArchiveSupplierCommand, bool>
{
    /// <inheritdoc />
    public Task<bool> Handle(ArchiveSupplierCommand request, CancellationToken cancellationToken) =>
        catalog.ArchiveAsync(request.WorkspaceId, request.SupplierId, timeProvider.GetUtcNow(), cancellationToken);
}
