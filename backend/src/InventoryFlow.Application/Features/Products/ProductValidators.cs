using FluentValidation;
using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Features.Products;

/// <summary>Validates product creation input.</summary>
public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    /// <summary>Initializes validation rules.</summary>
    public CreateProductCommandValidator()
    {
        RuleFor(command => command.WorkspaceId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Product.NameMaxLength);
        RuleFor(command => command.Sku).NotEmpty().MaximumLength(Product.SkuMaxLength);
    }
}
