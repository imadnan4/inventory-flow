using InventoryFlow.Application.Features.Suppliers;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InventoryFlow.Infrastructure.Suppliers;

/// <summary>Persists workspace-scoped suppliers with SQL Server uniqueness and lifecycle handling.</summary>
public sealed class EfSupplierCatalog(ApplicationDbContext dbContext) : ISupplierCatalog
{
    /// <inheritdoc />
    public async Task<Supplier> CreateAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        dbContext.Suppliers.Add(supplier);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return supplier;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new SupplierNameConflictException();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Supplier>> ListActiveAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        await dbContext.Suppliers.AsNoTracking().Where(supplier => supplier.WorkspaceId == workspaceId &&
            supplier.ArchivedAtUtc == null).OrderBy(supplier => supplier.Name).ThenBy(supplier => supplier.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<Supplier?> FindAsync(Guid workspaceId, Guid supplierId, CancellationToken cancellationToken) =>
        dbContext.Suppliers.SingleOrDefaultAsync(supplier => supplier.WorkspaceId == workspaceId && supplier.Id == supplierId,
            cancellationToken);

    /// <inheritdoc />
    public async Task<bool> ArchiveAsync(Guid workspaceId, Guid supplierId, DateTimeOffset archivedAtUtc,
        CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId &&
            item.Id == supplierId, cancellationToken);
        if (supplier is null) return false;
        supplier.Archive(archivedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await dbContext.SaveChangesAsync(cancellationToken);
}
