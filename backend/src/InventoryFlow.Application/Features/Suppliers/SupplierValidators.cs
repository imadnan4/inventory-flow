using FluentValidation;
using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Features.Suppliers;

/// <summary>Validates supplier creation input.</summary>
public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    /// <summary>Initializes validation rules.</summary>
    public CreateSupplierCommandValidator()
    {
        RuleFor(command => command.WorkspaceId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Supplier.NameMaxLength);
    }
}
