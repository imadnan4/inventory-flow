using System.Data;
using InventoryFlow.Application.Features.Purchases;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Inventory;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryFlow.Infrastructure.Purchases;

/// <summary>Posts immutable purchase receipts and their ledger entries in one transaction.</summary>
public sealed class EfPurchaseReceiptService(ApplicationDbContext dbContext) : IPurchaseReceiptService
{
    public async Task<PurchaseReceipt?> RecordAsync(Guid workspaceId, Guid supplierId, Guid warehouseId, Guid productId, decimal quantity,
        string idempotencyKey, DateTimeOffset receivedAtUtc, CancellationToken cancellationToken)
    {
        quantity = InventoryMovement.ValidateQuantity(quantity);
        idempotencyKey = InventoryMovement.NormalizeIdempotencyKey(idempotencyKey);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var existing = await dbContext.PurchaseReceipts.SingleOrDefaultAsync(receipt => receipt.WorkspaceId == workspaceId &&
                receipt.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return existing;
            }

            var supplierExists = await dbContext.Suppliers.AnyAsync(supplier => supplier.Id == supplierId &&
                supplier.WorkspaceId == workspaceId && supplier.ArchivedAtUtc == null, cancellationToken);
            if (!supplierExists)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            var receiptId = Guid.NewGuid();
            var movement = await new InventoryLedgerWriter(dbContext).RecordAsync(workspaceId, warehouseId, productId,
                InventoryMovementType.Receipt, quantity, receiptId.ToString("N"), receivedAtUtc, Guid.NewGuid(), cancellationToken);
            if (movement is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            var receipt = new PurchaseReceipt(receiptId, workspaceId, supplierId, warehouseId, productId, quantity, idempotencyKey,
                movement.Id, receivedAtUtc);
            dbContext.PurchaseReceipts.Add(receipt);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return receipt;
        });
    }

    public async Task<IReadOnlyList<PurchaseReceipt>> ListAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        await dbContext.PurchaseReceipts.AsNoTracking().Where(receipt => receipt.WorkspaceId == workspaceId)
            .OrderByDescending(receipt => receipt.ReceivedAtUtc).ThenByDescending(receipt => receipt.Id).ToListAsync(cancellationToken);
}
