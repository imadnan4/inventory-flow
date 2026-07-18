using InventoryFlow.Domain.Entities;
using MediatR;

namespace InventoryFlow.Application.Features.Products;

/// <summary>Handles product creation.</summary>
public sealed class CreateProductHandler(IProductCatalog catalog, TimeProvider timeProvider) : IRequestHandler<CreateProductCommand, ProductResponse>
{
    /// <inheritdoc />
    public async Task<ProductResponse> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = new Product(Guid.NewGuid(), request.WorkspaceId, request.Name, request.Sku, timeProvider.GetUtcNow());
        return ProductResponse.From(await catalog.CreateAsync(product, cancellationToken));
    }
}
/// <summary>Handles active-product listing.</summary>
public sealed class ListProductsHandler(IProductCatalog catalog) : IRequestHandler<ListProductsQuery, IReadOnlyList<ProductResponse>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductResponse>> Handle(ListProductsQuery request, CancellationToken cancellationToken) =>
        (await catalog.ListActiveAsync(request.WorkspaceId, cancellationToken)).Select(ProductResponse.From).ToArray();
}
/// <summary>Handles idempotent product archive.</summary>
public sealed class ArchiveProductHandler(IProductCatalog catalog, TimeProvider timeProvider) : IRequestHandler<ArchiveProductCommand, bool>
{
    /// <inheritdoc />
    public async Task<bool> Handle(ArchiveProductCommand request, CancellationToken cancellationToken)
    {
        var product = await catalog.FindAsync(request.WorkspaceId, request.ProductId, cancellationToken);
        if (product is null) return false;
        product.Archive(timeProvider.GetUtcNow());
        await catalog.SaveChangesAsync(cancellationToken);
        return true;
    }
}
