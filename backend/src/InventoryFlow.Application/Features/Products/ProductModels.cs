using InventoryFlow.Domain.Entities;
using MediatR;

namespace InventoryFlow.Application.Features.Products;

/// <summary>Supplies browser input for product creation.</summary>
public sealed record CreateProductRequest(string Name, string Sku);
/// <summary>Represents a product exposed to clients.</summary>
public sealed record ProductResponse(Guid Id, string Name, string Sku, DateTimeOffset CreatedAtUtc)
{
    /// <summary>Maps a domain product to a response.</summary>
    public static ProductResponse From(Product product) => new(product.Id, product.Name, product.Sku, product.CreatedAtUtc);
}
/// <summary>Creates a product in a server-resolved workspace.</summary>
public sealed record CreateProductCommand(Guid WorkspaceId, string Name, string Sku) : IRequest<ProductResponse>;
/// <summary>Lists active products in a server-resolved workspace.</summary>
public sealed record ListProductsQuery(Guid WorkspaceId) : IRequest<IReadOnlyList<ProductResponse>>;
/// <summary>Archives a product in a server-resolved workspace.</summary>
public sealed record ArchiveProductCommand(Guid WorkspaceId, Guid ProductId) : IRequest<bool>;
