using InventoryFlow.Domain.Common;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.Domain.Entities;

/// <summary>Represents a workspace-owned supplier catalog entry.</summary>
public sealed class Supplier : Entity<Guid>
{
    /// <summary>Maximum permitted supplier-name length.</summary>
    public const int NameMaxLength = 200;

    /// <summary>Initializes a supplier.</summary>
    public Supplier(Guid id, Guid workspaceId, string name, DateTimeOffset createdAtUtc) : base(id)
    {
        if (id == Guid.Empty) throw new DomainException("Supplier identifier is required.");
        if (workspaceId == Guid.Empty) throw new DomainException("Workspace identifier is required.");
        if (createdAtUtc.Offset != TimeSpan.Zero) throw new DomainException("Supplier creation time must be in UTC.");
        WorkspaceId = workspaceId;
        Name = NormalizeName(name);
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Gets the owning workspace identifier.</summary>
    public Guid WorkspaceId { get; private set; }
    /// <summary>Gets the normalized display name.</summary>
    public string Name { get; private set; }
    /// <summary>Gets the UTC creation instant.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }
    /// <summary>Gets the UTC archive instant, if archived.</summary>
    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    /// <summary>Normalizes and validates a name.</summary>
    public static string NormalizeName(string name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > NameMaxLength)
            throw new DomainException($"Supplier name must contain between 1 and {NameMaxLength} characters.");
        return normalized;
    }

    /// <summary>Archives the supplier once.</summary>
    public void Archive(DateTimeOffset archivedAtUtc)
    {
        if (archivedAtUtc.Offset != TimeSpan.Zero) throw new DomainException("Supplier archive time must be in UTC.");
        ArchivedAtUtc ??= archivedAtUtc;
    }
}
