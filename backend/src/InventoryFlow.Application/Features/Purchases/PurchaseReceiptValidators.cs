using FluentValidation;
using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Features.Purchases;

public sealed class RecordPurchaseReceiptCommandValidator : AbstractValidator<RecordPurchaseReceiptCommand>
{
    public RecordPurchaseReceiptCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0m).Must(quantity => decimal.Round(quantity, 4) == quantity)
            .WithMessage("Quantity must have at most four decimal places.")
            .LessThanOrEqualTo(InventoryMovement.MaxQuantity).WithMessage("Quantity must not exceed 99999999999999.9999.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(InventoryMovement.IdempotencyKeyMaxLength);
    }
}
