using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.Domain.Entities;

/// <summary>Represents the current on-hand quantity for a product at a warehouse.</summary>
public sealed class InventoryBalance
{
    /// <summary>Initializes a zero or positive inventory balance.</summary>
    public InventoryBalance(Guid workspaceId, Guid warehouseId, Guid productId, decimal quantity)
    {
        if (workspaceId == Guid.Empty || warehouseId == Guid.Empty || productId == Guid.Empty)
            throw new DomainException("Inventory balance identifiers are required.");
        if (quantity < 0m || decimal.Round(quantity, 4) != quantity || quantity > InventoryMovement.MaxQuantity)
            throw new DomainException("Inventory balance must be nonnegative, have at most four decimal places, and not exceed 99999999999999.9999.");
        WorkspaceId = workspaceId;
        WarehouseId = warehouseId;
        ProductId = productId;
        Quantity = quantity;
    }

    /// <summary>Gets the owning workspace identifier.</summary>
    public Guid WorkspaceId { get; private set; }
    /// <summary>Gets the warehouse identifier.</summary>
    public Guid WarehouseId { get; private set; }
    /// <summary>Gets the product identifier.</summary>
    public Guid ProductId { get; private set; }
    /// <summary>Gets the current on-hand quantity.</summary>
    public decimal Quantity { get; private set; }

    /// <summary>Applies a signed quantity change without allowing negative stock.</summary>
    public void Apply(decimal quantityDelta)
    {
        if (decimal.Round(quantityDelta, 4) != quantityDelta)
            throw new DomainException("Inventory quantity must have at most four decimal places.");
        var next = Quantity + quantityDelta;
        if (next < 0m) throw new InsufficientInventoryException();
        if (next > InventoryMovement.MaxQuantity)
            throw new DomainException("Inventory balance cannot exceed 99999999999999.9999.");
        Quantity = next;
    }
}

/// <summary>Indicates that an issue would make inventory negative.</summary>
public sealed class InsufficientInventoryException : Exception
{
    /// <summary>Initializes the exception.</summary>
    public InsufficientInventoryException() : base("Insufficient inventory is available for this issue.") { }
}
