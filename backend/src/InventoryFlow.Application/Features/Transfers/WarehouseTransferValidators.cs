using FluentValidation;
using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Features.Transfers;

public sealed class RecordWarehouseTransferCommandValidator : AbstractValidator<RecordWarehouseTransferCommand>
{
    public RecordWarehouseTransferCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.SourceWarehouseId).NotEmpty();
        RuleFor(x => x.DestinationWarehouseId).NotEmpty().NotEqual(x => x.SourceWarehouseId)
            .WithMessage("Source and destination warehouses must be different.");
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0m).Must(quantity => decimal.Round(quantity, 4) == quantity)
            .WithMessage("Quantity must have at most four decimal places.")
            .LessThanOrEqualTo(InventoryMovement.MaxQuantity).WithMessage("Quantity must not exceed 99999999999999.9999.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(InventoryMovement.IdempotencyKeyMaxLength);
    }
}
