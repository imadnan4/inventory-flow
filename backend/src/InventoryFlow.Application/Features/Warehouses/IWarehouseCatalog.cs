using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Features.Warehouses;

/// <summary>Provides workspace-scoped persistence operations for warehouses.</summary>
public interface IWarehouseCatalog
{
    /// <summary>Creates a warehouse.</summary>
    Task<Warehouse> CreateAsync(Warehouse warehouse, CancellationToken cancellationToken);
    /// <summary>Lists active warehouses.</summary>
    Task<IReadOnlyList<Warehouse>> ListActiveAsync(Guid workspaceId, CancellationToken cancellationToken);
    /// <summary>Finds a warehouse only within its workspace.</summary>
    Task<Warehouse?> FindAsync(Guid workspaceId, Guid warehouseId, CancellationToken cancellationToken);
    /// <summary>Archives a warehouse only when no nonzero workspace balance references it.</summary>
    Task<bool> ArchiveAsync(Guid workspaceId, Guid warehouseId, DateTimeOffset archivedAtUtc, CancellationToken cancellationToken);
    /// <summary>Persists pending changes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Represents a duplicate canonical warehouse name within a workspace.</summary>
public sealed class WarehouseNameConflictException : Exception
{
    /// <summary>Initializes the exception.</summary>
    public WarehouseNameConflictException() : base("A warehouse with this name already exists in the workspace.") { }
}
