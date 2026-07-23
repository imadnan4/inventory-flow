using InventoryFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryFlow.Infrastructure.Persistence.Configurations;

/// <summary>Configures workspace persistence.</summary>
public sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("Workspaces");
        builder.HasKey(workspace => workspace.Id);
        builder.Property(workspace => workspace.Name).HasMaxLength(Workspace.NameMaxLength).IsRequired();
        builder.Property(workspace => workspace.CreatedAtUtc).IsRequired();
    }
}
