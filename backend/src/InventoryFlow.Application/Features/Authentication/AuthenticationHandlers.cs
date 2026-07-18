using MediatR;

namespace InventoryFlow.Application.Features.Authentication;

/// <summary>Delegates registration to the authentication port.</summary>
public sealed class RegisterUserHandler(IAuthenticationService service) : IRequestHandler<RegisterUserCommand, AuthenticationSession>
{ public Task<AuthenticationSession> Handle(RegisterUserCommand request, CancellationToken cancellationToken) => service.RegisterAsync(request, cancellationToken); }
/// <summary>Delegates login to the authentication port.</summary>
public sealed class LoginUserHandler(IAuthenticationService service) : IRequestHandler<LoginUserCommand, AuthenticationSession>
{ public async Task<AuthenticationSession> Handle(LoginUserCommand request, CancellationToken cancellationToken) => await service.LoginAsync(request, cancellationToken) ?? throw new AuthenticationException(); }
/// <summary>Delegates refresh to the authentication port.</summary>
public sealed class RefreshSessionHandler(IAuthenticationService service) : IRequestHandler<RefreshSessionCommand, AuthenticationSession?>
{ public Task<AuthenticationSession?> Handle(RefreshSessionCommand request, CancellationToken cancellationToken) => service.RefreshAsync(request.RefreshToken, cancellationToken); }
/// <summary>Delegates logout to the authentication port.</summary>
public sealed class LogoutSessionHandler(IAuthenticationService service) : IRequestHandler<LogoutSessionCommand>
{ public async Task Handle(LogoutSessionCommand request, CancellationToken cancellationToken) => await service.LogoutAsync(request.RefreshToken, cancellationToken); }
/// <summary>Delegates current-user lookup to the authentication port.</summary>
public sealed class GetCurrentUserHandler(IAuthenticationService service) : IRequestHandler<GetCurrentUserQuery, AuthenticatedUser?>
{ public Task<AuthenticatedUser?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken) => service.GetUserAsync(request.UserId, cancellationToken); }

/// <summary>Represents an externally-uniform authentication failure.</summary>
public sealed class AuthenticationException : Exception
{
    /// <summary>Initializes the exception.</summary>
    public AuthenticationException() : base("Authentication failed.")
    {
    }
}
