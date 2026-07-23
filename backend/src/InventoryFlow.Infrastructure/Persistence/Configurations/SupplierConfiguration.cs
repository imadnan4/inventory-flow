using InventoryFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryFlow.Infrastructure.Persistence.Configurations;

/// <summary>Configures supplier persistence.</summary>
public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Suppliers");
        builder.HasKey(supplier => supplier.Id);
        builder.Property(supplier => supplier.Name).HasMaxLength(Supplier.NameMaxLength).IsRequired();
        builder.Property(supplier => supplier.CreatedAtUtc).IsRequired();
        builder.HasIndex(supplier => new { supplier.WorkspaceId, supplier.Name }).IsUnique();
        builder.HasIndex(supplier => new { supplier.WorkspaceId, supplier.ArchivedAtUtc, supplier.Name, supplier.Id });
        builder.HasOne<Workspace>().WithMany().HasForeignKey(supplier => supplier.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
    }
}
