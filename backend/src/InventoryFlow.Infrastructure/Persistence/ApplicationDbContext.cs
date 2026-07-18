using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InventoryFlow.Infrastructure.Persistence;

/// <summary>
/// Provides SQL Server persistence for Identity and Inventory Flow data.
/// </summary>
/// <param name="options">The options used to configure the context.</param>
public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    /// <summary>
    /// Gets the issued refresh tokens.
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    /// <summary>Gets workspaces.</summary>
    public DbSet<Workspace> Workspaces => Set<Workspace>();

    /// <summary>Gets workspace memberships.</summary>
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();

    /// <summary>Gets workspace-scoped products.</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>
    /// Gets the shared data-protection keys used by Identity token providers.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
