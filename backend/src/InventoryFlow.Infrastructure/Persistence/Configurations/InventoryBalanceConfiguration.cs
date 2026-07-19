using InventoryFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryFlow.Infrastructure.Persistence.Configurations;

/// <summary>Configures current warehouse-product inventory balances.</summary>
public sealed class InventoryBalanceConfiguration : IEntityTypeConfiguration<InventoryBalance>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InventoryBalance> builder)
    {
        builder.ToTable("InventoryBalances");
        builder.HasKey(balance => new { balance.WorkspaceId, balance.WarehouseId, balance.ProductId });
        builder.Property(balance => balance.Quantity).HasPrecision(18, 4).IsRequired();
        builder.HasOne<Workspace>().WithMany().HasForeignKey(balance => balance.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(balance => balance.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(balance => balance.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(balance => new { balance.WorkspaceId, balance.ProductId });
    }
}
