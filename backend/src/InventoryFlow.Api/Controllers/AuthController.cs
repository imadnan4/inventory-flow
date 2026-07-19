using System.Security.Claims;
using InventoryFlow.Application.Features.Authentication;
using InventoryFlow.Infrastructure.Authentication;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InventoryFlow.Api.Controllers;

/// <summary>Provides browser authentication endpoints.</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(ISender sender, IOptions<JwtOptions> options) : ControllerBase
{
    private readonly JwtOptions _options = options.Value;

    /// <summary>Registers a user and starts a browser session.</summary>
    [HttpPost("register")]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthenticationResponse>> Register(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var session = await sender.Send(command, cancellationToken);
        SetRefreshCookie(session.RefreshToken);
        return Ok(session.Response);
    }

    /// <summary>Authenticates a user and starts a browser session.</summary>
    [HttpPost("login")]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticationResponse>> Login(LoginUserCommand command, CancellationToken cancellationToken)
    {
        var session = await sender.Send(command, cancellationToken);
        SetRefreshCookie(session.RefreshToken);
        return Ok(session.Response);
    }

    /// <summary>Rotates the HttpOnly refresh cookie and returns a new access token.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticationResponse>> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[_options.RefreshCookieName];
        if (string.IsNullOrWhiteSpace(refreshToken)) { DeleteRefreshCookie(); return Unauthorized(); }
        var session = await sender.Send(new RefreshSessionCommand(refreshToken), cancellationToken);
        if (session is null) { DeleteRefreshCookie(); return Unauthorized(); }
        SetRefreshCookie(session.RefreshToken);
        return Ok(session.Response);
    }

    /// <summary>Switches the active workspace, rotating the persisted refresh session.</summary>
    [Authorize]
    [HttpPost("workspace/switch")]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticationResponse>> SwitchWorkspace(SwitchWorkspaceRequest request, CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[_options.RefreshCookieName];
        if (string.IsNullOrWhiteSpace(refreshToken)) { DeleteRefreshCookie(); return Unauthorized(); }
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId)) return Unauthorized();
        var session = await sender.Send(new SwitchWorkspaceCommand(userId, request.WorkspaceId, refreshToken), cancellationToken);
        if (session is null) return Forbid();
        SetRefreshCookie(session.RefreshToken);
        return Ok(session.Response);
    }

    /// <summary>Revokes the refresh family held by the browser.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await sender.Send(new LogoutSessionCommand(Request.Cookies[_options.RefreshCookieName]), cancellationToken);
        DeleteRefreshCookie();
        return NoContent();
    }

    /// <summary>Gets the current user from a valid bearer token.</summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<AuthenticatedUser>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthenticatedUser>> Me(CancellationToken cancellationToken)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var workspaceValue = User.FindFirstValue(JwtAccessTokenIssuer.WorkspaceIdClaimType);
        if (!Guid.TryParse(value, out var userId) || !Guid.TryParse(workspaceValue, out var workspaceId)) return Unauthorized();
        var user = await sender.Send(new GetCurrentUserQuery(userId, workspaceId), cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }

    private void SetRefreshCookie(string value) => Response.Cookies.Append(_options.RefreshCookieName, value, new CookieOptions
    { HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = !HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment(), Path = "/api/auth", MaxAge = TimeSpan.FromDays(_options.RefreshTokenLifetimeDays), Expires = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenLifetimeDays), IsEssential = true });
    private void DeleteRefreshCookie() => Response.Cookies.Delete(_options.RefreshCookieName, new CookieOptions { Path = "/api/auth", SameSite = SameSiteMode.Strict, Secure = !HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() });
}

/// <summary>Supplies an active-workspace switch request.</summary>
public sealed record SwitchWorkspaceRequest(Guid WorkspaceId);
