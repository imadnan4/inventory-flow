using InventoryFlow.Application.Features.Products;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InventoryFlow.Infrastructure.Products;

/// <summary>Persists workspace-scoped products with SQL Server uniqueness handling.</summary>
public sealed class EfProductCatalog(ApplicationDbContext dbContext) : IProductCatalog
{
    /// <inheritdoc />
    public async Task<Product> CreateAsync(Product product, CancellationToken cancellationToken)
    {
        dbContext.Products.Add(product);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return product;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ProductSkuConflictException();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Product>> ListActiveAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        await dbContext.Products.AsNoTracking().Where(item => item.WorkspaceId == workspaceId && item.ArchivedAtUtc == null)
            .OrderBy(item => item.Name).ThenBy(item => item.Id).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<Product?> FindAsync(Guid workspaceId, Guid productId, CancellationToken cancellationToken) =>
        dbContext.Products.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.Id == productId, cancellationToken);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await dbContext.SaveChangesAsync(cancellationToken);
}
