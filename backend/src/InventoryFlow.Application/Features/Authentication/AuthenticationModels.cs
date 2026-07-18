namespace InventoryFlow.Application.Features.Authentication;

/// <summary>Represents the authenticated workspace exposed to clients.</summary>
public sealed record AuthenticatedWorkspace(Guid Id, string Name);

/// <summary>Represents the authenticated user exposed to clients.</summary>
public sealed record AuthenticatedUser(Guid Id, string Email, string DisplayName, AuthenticatedWorkspace Workspace);

/// <summary>Represents a browser session response.</summary>
public sealed record AuthenticationResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, AuthenticatedUser User);

/// <summary>Represents the internal result used to write a refresh cookie.</summary>
public sealed record AuthenticationSession(AuthenticationResponse Response, string RefreshToken);

/// <summary>Supplies registration input.</summary>
public sealed record RegisterUserCommand(string DisplayName, string Email, string Password) : MediatR.IRequest<AuthenticationSession>;
/// <summary>Supplies login input.</summary>
public sealed record LoginUserCommand(string Email, string Password) : MediatR.IRequest<AuthenticationSession>;
/// <summary>Refreshes a browser session.</summary>
public sealed record RefreshSessionCommand(string RefreshToken) : MediatR.IRequest<AuthenticationSession?>;
/// <summary>Ends a browser session.</summary>
public sealed record LogoutSessionCommand(string? RefreshToken) : MediatR.IRequest;
/// <summary>Retrieves the current user.</summary>
public sealed record GetCurrentUserQuery(Guid UserId) : MediatR.IRequest<AuthenticatedUser?>;
