using System.Data;
using InventoryFlow.Application.Features.Transfers;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Domain.Exceptions;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryFlow.Infrastructure.Transfers;

/// <summary>Posts immutable warehouse transfers with a serializable, two-balance ledger update.</summary>
public sealed class EfWarehouseTransferService(ApplicationDbContext dbContext, IServiceScopeFactory scopeFactory) : IWarehouseTransferService
{
    public async Task<WarehouseTransfer?> RecordAsync(Guid workspaceId, Guid sourceWarehouseId, Guid destinationWarehouseId,
        Guid productId, decimal quantity, string idempotencyKey, DateTimeOffset transferredAtUtc, CancellationToken cancellationToken)
    {
        quantity = InventoryMovement.ValidateQuantity(quantity);
        idempotencyKey = InventoryMovement.NormalizeIdempotencyKey(idempotencyKey);
        if (sourceWarehouseId == destinationWarehouseId)
            throw new DomainException("Source and destination warehouses must be different.");

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var attemptDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await RecordAttemptAsync(attemptDbContext, workspaceId, sourceWarehouseId, destinationWarehouseId, productId,
                quantity, idempotencyKey, transferredAtUtc, cancellationToken);
        });
    }

    public async Task<IReadOnlyList<WarehouseTransfer>> ListAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        await dbContext.WarehouseTransfers.AsNoTracking().Where(transfer => transfer.WorkspaceId == workspaceId)
            .OrderByDescending(transfer => transfer.TransferredAtUtc).ThenByDescending(transfer => transfer.Id)
            .ToListAsync(cancellationToken);

    private static async Task<WarehouseTransfer?> RecordAttemptAsync(ApplicationDbContext dbContext, Guid workspaceId,
        Guid sourceWarehouseId, Guid destinationWarehouseId, Guid productId, decimal quantity, string idempotencyKey,
        DateTimeOffset transferredAtUtc, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var existing = await dbContext.WarehouseTransfers.SingleOrDefaultAsync(transfer => transfer.WorkspaceId == workspaceId &&
            transfer.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.SourceWarehouseId != sourceWarehouseId || existing.DestinationWarehouseId != destinationWarehouseId ||
                existing.ProductId != productId || existing.Quantity != quantity)
                throw new InvalidOperationException("Idempotency key reused with different parameters.");
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }

        var productExists = await dbContext.Products.AnyAsync(product => product.Id == productId &&
            product.WorkspaceId == workspaceId && product.ArchivedAtUtc == null, cancellationToken);
        var sourceExists = await dbContext.Warehouses.AnyAsync(warehouse => warehouse.Id == sourceWarehouseId &&
            warehouse.WorkspaceId == workspaceId && warehouse.ArchivedAtUtc == null, cancellationToken);
        var destinationExists = await dbContext.Warehouses.AnyAsync(warehouse => warehouse.Id == destinationWarehouseId &&
            warehouse.WorkspaceId == workspaceId && warehouse.ArchivedAtUtc == null, cancellationToken);
        if (!productExists || !sourceExists || !destinationExists)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        // Every transfer acquires both key/range locks in the same warehouse-ID order, including reverse transfers.
        var firstWarehouseId = sourceWarehouseId.CompareTo(destinationWarehouseId) < 0 ? sourceWarehouseId : destinationWarehouseId;
        var secondWarehouseId = firstWarehouseId == sourceWarehouseId ? destinationWarehouseId : sourceWarehouseId;
        var firstBalance = await LockBalanceAsync(dbContext, workspaceId, firstWarehouseId, productId, cancellationToken);
        var secondBalance = await LockBalanceAsync(dbContext, workspaceId, secondWarehouseId, productId, cancellationToken);

        var sourceBalance = sourceWarehouseId == firstWarehouseId ? firstBalance : secondBalance;
        var destinationBalance = destinationWarehouseId == firstWarehouseId ? firstBalance : secondBalance;
        sourceBalance.Apply(-quantity);
        destinationBalance.Apply(quantity);

        var transferId = Guid.NewGuid();
        var sourceMovement = new InventoryMovement(Guid.NewGuid(), workspaceId, sourceWarehouseId, productId,
            InventoryMovementType.Issue, quantity, $"{transferId:N}:issue", sourceBalance.Quantity, transferredAtUtc);
        var destinationMovement = new InventoryMovement(Guid.NewGuid(), workspaceId, destinationWarehouseId, productId,
            InventoryMovementType.Receipt, quantity, $"{transferId:N}:receipt", destinationBalance.Quantity, transferredAtUtc);
        var transfer = new WarehouseTransfer(transferId, workspaceId, sourceWarehouseId, destinationWarehouseId, productId,
            quantity, idempotencyKey, sourceMovement.Id, destinationMovement.Id, transferredAtUtc);

        dbContext.InventoryMovements.AddRange(sourceMovement, destinationMovement);
        dbContext.WarehouseTransfers.Add(transfer);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return transfer;
    }

    private static async Task<InventoryBalance> LockBalanceAsync(ApplicationDbContext dbContext, Guid workspaceId,
        Guid warehouseId, Guid productId, CancellationToken cancellationToken)
    {
        var balance = await dbContext.InventoryBalances.FromSqlInterpolated($"""
            SELECT * FROM [InventoryBalances] WITH (UPDLOCK, HOLDLOCK)
            WHERE [WorkspaceId] = {workspaceId} AND [WarehouseId] = {warehouseId} AND [ProductId] = {productId}
            """).SingleOrDefaultAsync(cancellationToken);
        if (balance is not null) return balance;

        balance = new InventoryBalance(workspaceId, warehouseId, productId, 0m);
        dbContext.InventoryBalances.Add(balance);
        return balance;
    }
}
