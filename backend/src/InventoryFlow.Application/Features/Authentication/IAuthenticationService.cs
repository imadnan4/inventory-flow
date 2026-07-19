namespace InventoryFlow.Application.Features.Authentication;

/// <summary>Defines authentication operations implemented by infrastructure.</summary>
public interface IAuthenticationService
{
    /// <summary>Registers a user and creates a session.</summary>
    Task<AuthenticationSession> RegisterAsync(RegisterUserCommand command, CancellationToken cancellationToken);
    /// <summary>Authenticates a user and creates a session.</summary>
    Task<AuthenticationSession?> LoginAsync(LoginUserCommand command, CancellationToken cancellationToken);
    /// <summary>Rotates a refresh token.</summary>
    Task<AuthenticationSession?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    /// <summary>Switches the active workspace and rotates the refresh token.</summary>
    Task<AuthenticationSession?> SwitchWorkspaceAsync(Guid userId, Guid workspaceId, string refreshToken, CancellationToken cancellationToken);
    /// <summary>Revokes the token family represented by a refresh token.</summary>
    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);
    /// <summary>Gets a current user by identifier and active workspace.</summary>
    Task<AuthenticatedUser?> GetUserAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken);
}
