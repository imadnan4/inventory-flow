using System.Data;
using InventoryFlow.Application.Features.Inventory;
using InventoryFlow.Application.Features.Warehouses;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InventoryFlow.Infrastructure.Warehouses;

/// <summary>Persists workspace-scoped warehouses with SQL Server lifecycle handling.</summary>
public sealed class EfWarehouseCatalog(ApplicationDbContext dbContext) : IWarehouseCatalog
{
    /// <inheritdoc />
    public async Task<Warehouse> CreateAsync(Warehouse warehouse, CancellationToken cancellationToken)
    {
        dbContext.Warehouses.Add(warehouse);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return warehouse;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new WarehouseNameConflictException();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Warehouse>> ListActiveAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        await dbContext.Warehouses.AsNoTracking().Where(warehouse => warehouse.WorkspaceId == workspaceId &&
            warehouse.ArchivedAtUtc == null).OrderBy(warehouse => warehouse.Name).ThenBy(warehouse => warehouse.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<Warehouse?> FindAsync(Guid workspaceId, Guid warehouseId, CancellationToken cancellationToken) =>
        dbContext.Warehouses.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.Id == warehouseId,
            cancellationToken);

    /// <inheritdoc />
    public async Task<bool> ArchiveAsync(Guid workspaceId, Guid warehouseId, DateTimeOffset archivedAtUtc,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var warehouse = await dbContext.Warehouses.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId &&
                item.Id == warehouseId, cancellationToken);
            if (warehouse is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            var hasOnHandBalance = await dbContext.InventoryBalances.AnyAsync(balance => balance.WorkspaceId == workspaceId &&
                balance.WarehouseId == warehouseId && balance.Quantity != 0m, cancellationToken);
            if (hasOnHandBalance)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new InventoryArchiveConflictException();
            }

            warehouse.Archive(archivedAtUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await dbContext.SaveChangesAsync(cancellationToken);
}
