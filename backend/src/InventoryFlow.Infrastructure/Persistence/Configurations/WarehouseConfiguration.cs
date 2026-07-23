using InventoryFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace InventoryFlow.Infrastructure.Persistence.Configurations; public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse> { public void Configure(EntityTypeBuilder<Warehouse> b) { ArgumentNullException.ThrowIfNull(b); b.ToTable("Warehouses"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(Warehouse.NameMaxLength).IsRequired(); b.HasIndex(x => new { x.WorkspaceId, x.Name }).IsUnique(); b.HasIndex(x => new { x.WorkspaceId, x.ArchivedAtUtc, x.Name }); b.HasOne<Workspace>().WithMany().HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Cascade); } }
