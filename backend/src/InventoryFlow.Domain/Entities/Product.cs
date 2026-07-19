using InventoryFlow.Domain.Common;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.Domain.Entities;

/// <summary>Represents a workspace-owned catalog product.</summary>
public sealed class Product : Entity<Guid>
{
    /// <summary>Maximum permitted product-name length.</summary>
    public const int NameMaxLength = 200;
    /// <summary>Maximum permitted canonical SKU length.</summary>
    public const int SkuMaxLength = 100;

    /// <summary>Initializes a product.</summary>
    public Product(Guid id, Guid workspaceId, string name, string sku, DateTimeOffset createdAtUtc) : base(id)
    {
        if (id == Guid.Empty) throw new DomainException("Product identifier is required.");
        if (workspaceId == Guid.Empty) throw new DomainException("Workspace identifier is required.");
        if (createdAtUtc.Offset != TimeSpan.Zero) throw new DomainException("Product creation time must be in UTC.");
        WorkspaceId = workspaceId;
        Name = NormalizeName(name);
        Sku = NormalizeSku(sku);
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Gets the owning workspace identifier.</summary>
    public Guid WorkspaceId { get; private set; }
    /// <summary>Gets the display name.</summary>
    public string Name { get; private set; }
    /// <summary>Gets the canonical stock-keeping unit.</summary>
    public string Sku { get; private set; }
    /// <summary>Gets the UTC creation instant.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }
    /// <summary>Gets the UTC archive instant, if archived.</summary>
    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    /// <summary>Normalizes and validates a name.</summary>
    public static string NormalizeName(string name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > NameMaxLength)
            throw new DomainException($"Product name must contain between 1 and {NameMaxLength} characters.");
        return normalized;
    }

    /// <summary>Normalizes and validates a SKU.</summary>
    public static string NormalizeSku(string sku)
    {
        var normalized = sku?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > SkuMaxLength)
            throw new DomainException($"Product SKU must contain between 1 and {SkuMaxLength} characters.");
        return normalized;
    }

    /// <summary>Archives the product once.</summary>
    public void Archive(DateTimeOffset archivedAtUtc)
    {
        if (archivedAtUtc.Offset != TimeSpan.Zero) throw new DomainException("Product archive time must be in UTC.");
        ArchivedAtUtc ??= archivedAtUtc;
    }
}
