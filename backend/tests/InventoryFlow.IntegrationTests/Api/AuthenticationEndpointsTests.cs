using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryFlow.Application.Features.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryFlow.IntegrationTests.Api;

/// <summary>
/// Verifies authentication against an isolated SQL Server instance.
/// </summary>
public sealed class AuthenticationEndpointsTests : IClassFixture<AuthenticatedApiFixture>
{
    private const string RefreshCookieName = "inventory_flow_refresh";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;
    private readonly AuthenticatedApiFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationEndpointsTests"/> class.
    /// </summary>
    /// <param name="fixture">The SQL Server-backed API fixture.</param>
    public AuthenticationEndpointsTests(AuthenticatedApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
        });
    }

    /// <summary>
    /// Registers a user and writes a secure browser-scoped refresh cookie.
    /// </summary>
    [Fact]
    public async Task Register_WritesExpectedRefreshCookie()
    {
        // Act
        using var response = await RegisterAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = GetRefreshCookieHeader(response);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Authorizes the current-user endpoint with the registration JWT.
    /// </summary>
    [Fact]
    public async Task Me_WithIssuedAccessToken_ReturnsCurrentUser()
    {
        // Arrange
        using var registration = await RegisterAsync();
        var session = await ReadSessionAsync(registration);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        // Act
        using var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<AuthenticatedUser>(JsonOptions);
        Assert.NotNull(user);
        Assert.Equal(session.User.Email, user.Email);
        Assert.Equal(session.User.Workspace, user.Workspace);
    }

    /// <summary>Provisions exactly one Owner workspace with registration.</summary>
    [Fact]
    public async Task Register_ProvisionsOwnerWorkspace()
    {
        using var registration = await RegisterAsync();
        var session = await ReadSessionAsync(registration);
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(1, await dbContext.Workspaces.CountAsync(workspace => workspace.Id == session.User.Workspace.Id));
        Assert.Equal(1, await dbContext.WorkspaceMembers.CountAsync(member => member.UserId == session.User.Id && member.WorkspaceId == session.User.Workspace.Id));
    }

    /// <summary>
    /// Rotates refresh tokens and invalidates an entire family after replay.
    /// </summary>
    [Fact]
    public async Task Refresh_ReplayRevokesEntireTokenFamily()
    {
        // Arrange
        using var registration = await RegisterAsync();
        var originalCookie = GetRefreshCookieValue(registration);

        // Act
        using var rotated = await RefreshAsync(originalCookie);
        var rotatedCookie = GetRefreshCookieValue(rotated);
        using var replay = await RefreshAsync(originalCookie);
        using var descendant = await RefreshAsync(rotatedCookie);

        // Assert
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, descendant.StatusCode);
    }

    /// <summary>
    /// Revokes the browser's refresh-token family on logout.
    /// </summary>
    [Fact]
    public async Task Logout_RevokesRefreshTokenFamily()
    {
        // Arrange
        using var registration = await RegisterAsync();
        var refreshCookie = GetRefreshCookieValue(registration);

        // Act
        using var logout = await SendWithRefreshCookieAsync(HttpMethod.Post, "/api/auth/logout", refreshCookie);
        using var refresh = await RefreshAsync(refreshCookie);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    /// <summary>
    /// Permits only one concurrent refresh and treats the other request as replay.
    /// </summary>
    [Fact]
    public async Task Refresh_ConcurrentRequests_OnlyOneSucceedsAndRevokesFamily()
    {
        // Arrange
        using var registration = await RegisterAsync();
        var originalCookie = GetRefreshCookieValue(registration);

        // Act
        var refreshes = await Task.WhenAll(
            RefreshAsync(originalCookie),
            RefreshAsync(originalCookie));
        using var first = refreshes[0];
        using var second = refreshes[1];
        var successfulRefresh = refreshes.Single(response => response.StatusCode == HttpStatusCode.OK);
        var replacementCookie = GetRefreshCookieValue(successfulRefresh);
        using var descendant = await RefreshAsync(replacementCookie);

        // Assert
        Assert.Single(refreshes, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(refreshes, response => response.StatusCode == HttpStatusCode.Unauthorized);
        Assert.Equal(HttpStatusCode.Unauthorized, descendant.StatusCode);
    }

    /// <summary>
    /// Revokes the original refresh token and any replacement issued while logout and refresh overlap.
    /// </summary>
    [Fact]
    public async Task Logout_ConcurrentWithRefresh_RevokesOriginalAndAnyReplacement()
    {
        // Arrange
        using var registration = await RegisterAsync();
        var originalCookie = GetRefreshCookieValue(registration);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var logoutTask = StartWhenReleasedAsync(
            start.Task,
            () => SendWithRefreshCookieAsync(HttpMethod.Post, "/api/auth/logout", originalCookie));
        var refreshTask = StartWhenReleasedAsync(start.Task, () => RefreshAsync(originalCookie));
        start.SetResult();

        using var logout = await logoutTask;
        using var refresh = await refreshTask;
        var issuedCookies = new List<string> { originalCookie };
        if (refresh.StatusCode == HttpStatusCode.OK)
        {
            issuedCookies.Add(GetRefreshCookieValue(refresh));
        }

        var refreshAttempts = await Task.WhenAll(issuedCookies.Select(RefreshAsync));
        using var originalAttempt = refreshAttempts[0];
        if (refreshAttempts.Length > 1)
        {
            using var replacementAttempt = refreshAttempts[1];
            Assert.Equal(HttpStatusCode.Unauthorized, replacementAttempt.StatusCode);
        }

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.True(
            refresh.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized,
            $"Expected refresh to succeed or be unauthorized, but received {refresh.StatusCode}.");
        Assert.Equal(HttpStatusCode.Unauthorized, originalAttempt.StatusCode);
    }

    private static async Task<T> StartWhenReleasedAsync<T>(Task start, Func<Task<T>> operation)
    {
        await start;
        return await operation();
    }

    private async Task<HttpResponseMessage> RegisterAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        return await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterUserCommand($"User {suffix}", $"user-{suffix}@example.test", "Password!12345"));
    }

    private async Task<HttpResponseMessage> RefreshAsync(string refreshCookie) =>
        await SendWithRefreshCookieAsync(HttpMethod.Post, "/api/auth/refresh", refreshCookie);

    private async Task<HttpResponseMessage> SendWithRefreshCookieAsync(
        HttpMethod method,
        string path,
        string refreshCookie)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", $"{RefreshCookieName}={refreshCookie}");
        return await _client.SendAsync(request);
    }

    private static async Task<AuthenticationResponse> ReadSessionAsync(HttpResponseMessage response)
    {
        var session = await response.Content.ReadFromJsonAsync<AuthenticationResponse>(JsonOptions);
        return Assert.IsType<AuthenticationResponse>(session);
    }

    private static string GetRefreshCookieHeader(HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues("Set-Cookie"));

    private static string GetRefreshCookieValue(HttpResponseMessage response)
    {
        var header = GetRefreshCookieHeader(response);
        var cookie = header.Split(';', 2)[0].Split('=', 2);
        Assert.Equal(RefreshCookieName, cookie[0]);
        return Uri.UnescapeDataString(cookie[1]);
    }
}
