using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryFlow.Infrastructure.Persistence.Configurations;

/// <summary>Configures workspace invitation persistence.</summary>
public sealed class WorkspaceInvitationConfiguration : IEntityTypeConfiguration<WorkspaceInvitation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WorkspaceInvitation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("WorkspaceInvitations");
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.NormalizedEmail).HasMaxLength(WorkspaceInvitation.NormalizedEmailMaxLength).IsRequired();
        builder.Property(invitation => invitation.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(invitation => invitation.TokenHash).HasMaxLength(WorkspaceInvitation.TokenHashMaxLength).IsRequired();
        builder.Property(invitation => invitation.ExpiresAtUtc).IsRequired();
        builder.Property(invitation => invitation.CreatedAtUtc).IsRequired();
        builder.Property(invitation => invitation.AcceptedAtUtc);
        builder.Property(invitation => invitation.AcceptedByUserId);
        builder.Property(invitation => invitation.RevokedAtUtc);
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasIndex(invitation => new { invitation.WorkspaceId, invitation.NormalizedEmail })
            .IsUnique()
            .HasFilter("[AcceptedAtUtc] IS NULL AND [RevokedAtUtc] IS NULL AND [ExpiresAtUtc] > GETUTCDATE()");
        builder.HasIndex(invitation => new { invitation.WorkspaceId, invitation.CreatedAtUtc });
        builder.HasOne<Workspace>().WithMany().HasForeignKey(invitation => invitation.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(invitation => invitation.CreatedByUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(invitation => invitation.AcceptedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
