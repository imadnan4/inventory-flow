namespace InventoryFlow.Application.Features.Inventory;

/// <summary>Indicates an inventory source cannot be archived while stock remains on hand.</summary>
public sealed class InventoryArchiveConflictException : Exception
{
    /// <summary>Initializes the exception with a safe client-facing message.</summary>
    public InventoryArchiveConflictException() : base("This product or warehouse cannot be archived while it has inventory on hand.") { }
}
