using InventoryFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryFlow.Infrastructure.Persistence.Configurations;

/// <summary>Configures product persistence.</summary>
public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Products");
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Name).HasMaxLength(Product.NameMaxLength).IsRequired();
        builder.Property(product => product.Sku).HasMaxLength(Product.SkuMaxLength).IsRequired();
        builder.Property(product => product.CreatedAtUtc).IsRequired();
        builder.HasIndex(product => new { product.WorkspaceId, product.Sku }).IsUnique();
        builder.HasIndex(product => new { product.WorkspaceId, product.ArchivedAtUtc, product.Name, product.Id });
        builder.HasOne<Workspace>().WithMany().HasForeignKey(product => product.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
    }
}
