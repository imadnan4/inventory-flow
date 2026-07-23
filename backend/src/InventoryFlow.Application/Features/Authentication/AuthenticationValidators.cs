using FluentValidation;

namespace InventoryFlow.Application.Features.Authentication;

/// <summary>Validates registration input.</summary>
public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    /// <summary>Initializes validation rules.</summary>
    public RegisterUserCommandValidator()
    {
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(200).Must(name => name is not null && name == name.Trim()).WithMessage("Display name cannot have leading or trailing whitespace.");
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(command => command.Password).NotEmpty();
    }
}
/// <summary>Validates login input.</summary>
public sealed class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{ public LoginUserCommandValidator() { RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(256); RuleFor(command => command.Password).NotEmpty(); } }
/// <summary>Validates refresh input.</summary>
public sealed class RefreshSessionCommandValidator : AbstractValidator<RefreshSessionCommand>
{ public RefreshSessionCommandValidator() => RuleFor(command => command.RefreshToken).NotEmpty(); }
/// <summary>Validates current-user lookup input.</summary>
public sealed class GetCurrentUserQueryValidator : AbstractValidator<GetCurrentUserQuery>
{
    public GetCurrentUserQueryValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
        RuleFor(query => query.WorkspaceId).NotEmpty();
    }
}
/// <summary>Validates workspace switch input.</summary>
public sealed class SwitchWorkspaceCommandValidator : AbstractValidator<SwitchWorkspaceCommand>
{
    public SwitchWorkspaceCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.WorkspaceId).NotEmpty();
        RuleFor(command => command.RefreshToken).NotEmpty();
    }
}
