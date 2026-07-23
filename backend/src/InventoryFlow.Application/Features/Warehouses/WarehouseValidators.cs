using FluentValidation;
using InventoryFlow.Domain.Entities;
namespace InventoryFlow.Application.Features.Warehouses; public sealed class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand> { public CreateWarehouseCommandValidator() { RuleFor(x => x.WorkspaceId).NotEmpty(); RuleFor(x => x.Name).NotEmpty().MaximumLength(Warehouse.NameMaxLength).Must(name => name.Trim().Length <= Warehouse.NameMaxLength).WithMessage("Warehouse name is too long after trimming."); } }
