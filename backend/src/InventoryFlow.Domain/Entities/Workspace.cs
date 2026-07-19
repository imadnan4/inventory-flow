using InventoryFlow.Domain.Common;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.Domain.Entities;

/// <summary>Represents a workspace that owns future Inventory Flow data.</summary>
public sealed class Workspace : Entity<Guid>
{
    /// <summary>Maximum permitted workspace-name length.</summary>
    public const int NameMaxLength = 120;

    /// <summary>Initializes a workspace.</summary>
    public Workspace(Guid id, string name, DateTimeOffset createdAtUtc) : base(id)
    {
        if (id == Guid.Empty) throw new DomainException("Workspace identifier is required.");
        Name = NormalizeName(name);
        if (createdAtUtc.Offset != TimeSpan.Zero) throw new DomainException("Workspace creation time must be in UTC.");
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Gets the workspace display name.</summary>
    public string Name { get; private set; }
    /// <summary>Gets the UTC creation instant.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Normalizes and validates a workspace display name.</summary>
    public static string NormalizeName(string name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > NameMaxLength)
            throw new DomainException($"Workspace name must contain between 1 and {NameMaxLength} characters.");
        return normalized;
    }
}
