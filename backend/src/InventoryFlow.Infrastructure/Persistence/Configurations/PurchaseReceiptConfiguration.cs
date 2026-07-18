using InventoryFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryFlow.Infrastructure.Persistence.Configurations;

public sealed class PurchaseReceiptConfiguration : IEntityTypeConfiguration<PurchaseReceipt>
{
    public void Configure(EntityTypeBuilder<PurchaseReceipt> builder)
    {
        builder.ToTable("PurchaseReceipts");
        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.Quantity).HasPrecision(18, 4).IsRequired();
        builder.Property(receipt => receipt.IdempotencyKey).HasMaxLength(InventoryMovement.IdempotencyKeyMaxLength).IsRequired();
        builder.Property(receipt => receipt.ReceivedAtUtc).IsRequired();
        builder.HasIndex(receipt => new { receipt.WorkspaceId, receipt.IdempotencyKey }).IsUnique();
        builder.HasIndex(receipt => receipt.InventoryMovementId).IsUnique();
        builder.HasIndex(receipt => new { receipt.WorkspaceId, receipt.ReceivedAtUtc, receipt.Id });
        builder.HasOne<Workspace>().WithMany().HasForeignKey(receipt => receipt.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(receipt => receipt.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(receipt => receipt.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(receipt => receipt.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryMovement>().WithMany().HasForeignKey(receipt => receipt.InventoryMovementId).OnDelete(DeleteBehavior.Restrict);
    }
}
