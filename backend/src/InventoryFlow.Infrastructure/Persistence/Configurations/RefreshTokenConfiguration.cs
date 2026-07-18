using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures persistence for <see cref="RefreshToken"/> entities.
/// </summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(token => token.Id);

        builder.Property(token => token.FamilyId)
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(token => token.ExpiresAtUtc)
            .IsRequired();

        builder.HasIndex(token => token.TokenHash)
            .IsUnique();

        builder.HasIndex(token => new { token.UserId, token.ExpiresAtUtc });

        builder.HasIndex(token => token.FamilyId);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
