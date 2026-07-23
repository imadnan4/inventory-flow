using InventoryFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryFlow.Infrastructure.Persistence.Configurations;

/// <summary>Configures immutable inventory ledger entries.</summary>
public sealed class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("InventoryMovements");
        builder.HasKey(movement => movement.Id);
        builder.Property(movement => movement.Type).HasConversion<int>().IsRequired();
        builder.Property(movement => movement.Quantity).HasPrecision(18, 4).IsRequired();
        builder.Property(movement => movement.BalanceAfterQuantity).HasPrecision(18, 4).IsRequired();
        builder.Property(movement => movement.IdempotencyKey).HasMaxLength(InventoryMovement.IdempotencyKeyMaxLength).IsRequired();
        builder.Property(movement => movement.OccurredAtUtc).IsRequired();
        builder.HasIndex(movement => new { movement.WorkspaceId, movement.IdempotencyKey }).IsUnique();
        builder.HasIndex(movement => new { movement.WorkspaceId, movement.WarehouseId, movement.ProductId, movement.OccurredAtUtc });
        builder.HasOne<Workspace>().WithMany().HasForeignKey(movement => movement.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(movement => movement.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(movement => movement.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
