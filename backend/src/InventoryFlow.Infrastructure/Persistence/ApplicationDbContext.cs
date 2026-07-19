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

    /// <summary>Gets workspace-scoped suppliers.</summary>
    public DbSet<Supplier> Suppliers => Set<Supplier>();

    /// <summary>Gets immutable inventory ledger movements.</summary>
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    /// <summary>Gets immutable supplier-linked purchase receipts.</summary>
    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();

    /// <summary>Gets immutable sales fulfillments.</summary>
    public DbSet<SalesFulfillment> SalesFulfillments => Set<SalesFulfillment>();

    /// <summary>Gets immutable warehouse transfers.</summary>
    public DbSet<WarehouseTransfer> WarehouseTransfers => Set<WarehouseTransfer>();

    /// <summary>Gets current inventory balances.</summary>
    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();

    /// <summary>Gets workspaces.</summary>
    public DbSet<Workspace> Workspaces => Set<Workspace>();

    /// <summary>Gets workspace memberships.</summary>
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();

    /// <summary>Gets workspace invitations.</summary>
    public DbSet<WorkspaceInvitation> WorkspaceInvitations => Set<WorkspaceInvitation>();

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
