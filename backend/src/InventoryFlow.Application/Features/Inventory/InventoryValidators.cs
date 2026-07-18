using FluentValidation;
using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Features.Inventory;

/// <summary>Validates inventory recording input.</summary>
public sealed class RecordReceiptCommandValidator : AbstractValidator<RecordReceiptCommand>
{
    /// <summary>Initializes validation rules.</summary>
    public RecordReceiptCommandValidator() => InventoryValidation.Configure(this);
}

/// <summary>Validates inventory issue input.</summary>
public sealed class RecordIssueCommandValidator : AbstractValidator<RecordIssueCommand>
{
    /// <summary>Initializes validation rules.</summary>
    public RecordIssueCommandValidator() => InventoryValidation.Configure(this);
}

internal static class InventoryValidation
{
    internal static void Configure<T>(AbstractValidator<T> validator) where T : class, IInventoryMovementCommand
    {
        validator.RuleFor(x => x.WorkspaceId).NotEmpty();
        validator.RuleFor(x => x.WarehouseId).NotEmpty();
        validator.RuleFor(x => x.ProductId).NotEmpty();
        validator.RuleFor(x => x.Quantity).GreaterThan(0m).Must(quantity => decimal.Round(quantity, 4) == quantity)
            .WithMessage("Quantity must have at most four decimal places.")
            .LessThanOrEqualTo(InventoryMovement.MaxQuantity)
            .WithMessage("Quantity must not exceed 99999999999999.9999.");
        validator.RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(InventoryMovement.IdempotencyKeyMaxLength);
    }
}
