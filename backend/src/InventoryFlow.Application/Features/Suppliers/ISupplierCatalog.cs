using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Features.Suppliers;

/// <summary>Provides workspace-scoped persistence operations for suppliers.</summary>
public interface ISupplierCatalog
{
    /// <summary>Creates a supplier.</summary>
    Task<Supplier> CreateAsync(Supplier supplier, CancellationToken cancellationToken);
    /// <summary>Lists active suppliers.</summary>
    Task<IReadOnlyList<Supplier>> ListActiveAsync(Guid workspaceId, CancellationToken cancellationToken);
    /// <summary>Finds a supplier only within its workspace.</summary>
    Task<Supplier?> FindAsync(Guid workspaceId, Guid supplierId, CancellationToken cancellationToken);
    /// <summary>Idempotently archives a supplier only within its workspace.</summary>
    Task<bool> ArchiveAsync(Guid workspaceId, Guid supplierId, DateTimeOffset archivedAtUtc, CancellationToken cancellationToken);
    /// <summary>Persists pending changes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Represents a duplicate normalized supplier name within a workspace.</summary>
public sealed class SupplierNameConflictException : Exception
{
    /// <summary>Initializes the exception.</summary>
    public SupplierNameConflictException() : base("A supplier with this name already exists in the workspace.") { }
}
