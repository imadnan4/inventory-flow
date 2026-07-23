using InventoryFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryFlow.Infrastructure.Persistence.Configurations;

public sealed class WarehouseTransferConfiguration : IEntityTypeConfiguration<WarehouseTransfer>
{
    public void Configure(EntityTypeBuilder<WarehouseTransfer> builder)
    {
        builder.ToTable("WarehouseTransfers");
        builder.HasKey(transfer => transfer.Id);
        builder.Property(transfer => transfer.Quantity).HasPrecision(18, 4).IsRequired();
        builder.Property(transfer => transfer.IdempotencyKey).HasMaxLength(InventoryMovement.IdempotencyKeyMaxLength).IsRequired();
        builder.Property(transfer => transfer.TransferredAtUtc).IsRequired();
        builder.HasIndex(transfer => new { transfer.WorkspaceId, transfer.IdempotencyKey }).IsUnique();
        builder.HasIndex(transfer => transfer.SourceInventoryMovementId).IsUnique();
        builder.HasIndex(transfer => transfer.DestinationInventoryMovementId).IsUnique();
        builder.HasIndex(transfer => new { transfer.WorkspaceId, transfer.TransferredAtUtc, transfer.Id });
        builder.HasOne<Workspace>().WithMany().HasForeignKey(transfer => transfer.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(transfer => transfer.SourceWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(transfer => transfer.DestinationWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(transfer => transfer.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryMovement>().WithMany().HasForeignKey(transfer => transfer.SourceInventoryMovementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryMovement>().WithMany().HasForeignKey(transfer => transfer.DestinationInventoryMovementId).OnDelete(DeleteBehavior.Restrict);
    }
}
