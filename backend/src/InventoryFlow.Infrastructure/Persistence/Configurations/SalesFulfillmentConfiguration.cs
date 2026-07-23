using InventoryFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryFlow.Infrastructure.Persistence.Configurations;

public sealed class SalesFulfillmentConfiguration : IEntityTypeConfiguration<SalesFulfillment>
{
    public void Configure(EntityTypeBuilder<SalesFulfillment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("SalesFulfillments");
        builder.HasKey(fulfillment => fulfillment.Id);
        builder.Property(fulfillment => fulfillment.Quantity).HasPrecision(18, 4).IsRequired();
        builder.Property(fulfillment => fulfillment.IdempotencyKey).HasMaxLength(InventoryMovement.IdempotencyKeyMaxLength).IsRequired();
        builder.Property(fulfillment => fulfillment.FulfilledAtUtc).IsRequired();
        builder.HasIndex(fulfillment => new { fulfillment.WorkspaceId, fulfillment.IdempotencyKey }).IsUnique();
        builder.HasIndex(fulfillment => fulfillment.InventoryMovementId).IsUnique();
        builder.HasIndex(fulfillment => new { fulfillment.WorkspaceId, fulfillment.FulfilledAtUtc, fulfillment.Id });
        builder.HasOne<Workspace>().WithMany().HasForeignKey(fulfillment => fulfillment.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(fulfillment => fulfillment.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(fulfillment => fulfillment.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryMovement>().WithMany().HasForeignKey(fulfillment => fulfillment.InventoryMovementId).OnDelete(DeleteBehavior.Restrict);
    }
}
