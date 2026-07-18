using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Features.Products;

/// <summary>Provides workspace-scoped persistence operations for products.</summary>
public interface IProductCatalog
{
    /// <summary>Creates a product.</summary>
    Task<Product> CreateAsync(Product product, CancellationToken cancellationToken);
    /// <summary>Lists active products.</summary>
    Task<IReadOnlyList<Product>> ListActiveAsync(Guid workspaceId, CancellationToken cancellationToken);
    /// <summary>Finds a product only within its workspace.</summary>
    Task<Product?> FindAsync(Guid workspaceId, Guid productId, CancellationToken cancellationToken);
    /// <summary>Persists pending changes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Represents a duplicate canonical SKU within a workspace.</summary>
public sealed class ProductSkuConflictException : Exception
{
    /// <summary>Initializes the exception.</summary>
    public ProductSkuConflictException() : base("A product with this SKU already exists in the workspace.") { }
}
