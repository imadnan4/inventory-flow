using InventoryFlow.Domain.Common;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.Domain.Entities;

/// <summary>Identifies the direction of an inventory ledger entry.</summary>
public enum InventoryMovementType
{
    Receipt = 1,
    Issue = 2
}

/// <summary>Represents an immutable, idempotent inventory ledger entry.</summary>
public sealed class InventoryMovement : Entity<Guid>
{
    /// <summary>Maximum quantity representable by the inventory decimal(18,4) columns.</summary>
    public const decimal MaxQuantity = 99999999999999.9999m;

    /// <summary>Maximum length of a client supplied idempotency key.</summary>
    public const int IdempotencyKeyMaxLength = 100;

    /// <summary>Initializes an inventory movement.</summary>
    public InventoryMovement(Guid id, Guid workspaceId, Guid warehouseId, Guid productId, InventoryMovementType type,
        decimal quantity, string idempotencyKey, decimal balanceAfterQuantity, DateTimeOffset occurredAtUtc) : base(id)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || warehouseId == Guid.Empty || productId == Guid.Empty)
            throw new DomainException("Inventory movement identifiers are required.");
        if (!Enum.IsDefined(type)) throw new DomainException("Inventory movement type is invalid.");
        Quantity = ValidateQuantity(quantity);
        IdempotencyKey = NormalizeIdempotencyKey(idempotencyKey);
        if (balanceAfterQuantity < 0m || decimal.Round(balanceAfterQuantity, 4) != balanceAfterQuantity ||
            balanceAfterQuantity > MaxQuantity)
            throw new DomainException("Inventory balance must be nonnegative, have at most four decimal places, and not exceed 99999999999999.9999.");
        if (occurredAtUtc.Offset != TimeSpan.Zero) throw new DomainException("Inventory movement time must be in UTC.");

        WorkspaceId = workspaceId;
        WarehouseId = warehouseId;
        ProductId = productId;
        Type = type;
        BalanceAfterQuantity = balanceAfterQuantity;
        OccurredAtUtc = occurredAtUtc;
    }

    /// <summary>Gets the owning workspace identifier.</summary>
    public Guid WorkspaceId { get; private set; }
    /// <summary>Gets the warehouse identifier.</summary>
    public Guid WarehouseId { get; private set; }
    /// <summary>Gets the product identifier.</summary>
    public Guid ProductId { get; private set; }
    /// <summary>Gets the direction of the movement.</summary>
    public InventoryMovementType Type { get; private set; }
    /// <summary>Gets the positive movement quantity.</summary>
    public decimal Quantity { get; private set; }
    /// <summary>Gets the client-supplied idempotency key.</summary>
    public string IdempotencyKey { get; private set; }
    /// <summary>Gets the resulting balance immediately after this movement.</summary>
    public decimal BalanceAfterQuantity { get; private set; }
    /// <summary>Gets the UTC instant at which the movement was recorded.</summary>
    public DateTimeOffset OccurredAtUtc { get; private set; }

    /// <summary>Validates a positive decimal quantity representable by inventory decimal(18,4) columns.</summary>
    public static decimal ValidateQuantity(decimal quantity)
    {
        if (quantity <= 0m || decimal.Round(quantity, 4) != quantity || quantity > MaxQuantity)
            throw new DomainException("Inventory quantity must be positive, have at most four decimal places, and not exceed 99999999999999.9999.");
        return quantity;
    }

    /// <summary>Normalizes and validates an idempotency key.</summary>
    public static string NormalizeIdempotencyKey(string idempotencyKey)
    {
        var value = idempotencyKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > IdempotencyKeyMaxLength)
            throw new DomainException($"Idempotency key must contain between 1 and {IdempotencyKeyMaxLength} characters.");
        return value;
    }
}
