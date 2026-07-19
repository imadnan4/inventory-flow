using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryFlow.Infrastructure.Persistence.Configurations;

/// <summary>Configures workspace membership persistence.</summary>
public sealed class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        builder.ToTable("WorkspaceMembers");
        builder.HasKey(member => member.Id);
        builder.Property(member => member.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(member => member.CreatedAtUtc).IsRequired();
        builder.HasIndex(member => new { member.WorkspaceId, member.UserId }).IsUnique();
        builder.HasIndex(member => member.UserId);
        builder.HasIndex(member => member.WorkspaceId);
        builder.HasOne<Workspace>().WithMany().HasForeignKey(member => member.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
