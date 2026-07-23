using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using InventoryFlow.Application.Features.Authentication;
using InventoryFlow.Application.Features.Collaboration;
using InventoryFlow.Application.Features.Products;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Authentication;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace InventoryFlow.IntegrationTests.Api;

/// <summary>Verifies workspace collaboration endpoints against SQL Server.</summary>
public sealed class CollaborationEndpointsTests : IClassFixture<AuthenticatedApiFixture>
{
    private const string RefreshCookieName = "inventory_flow_refresh";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;
    private readonly AuthenticatedApiFixture _fixture;

    /// <summary>Initializes the test class.</summary>
    public CollaborationEndpointsTests(AuthenticatedApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    /// <summary>Owner can invite an existing user, stores only token hash, and invited user can accept.</summary>
    [Fact]
    public async Task Invitation_CreateAndAccept_StoresHashOnlyAndCreatesMember()
    {
        var owner = await RegisterAsync("owner");
        var invited = await RegisterAsync("invited");
        var created = await CreateInvitationAsync(owner.Session.AccessToken, invited.Session.User.Email);

        Assert.False(string.IsNullOrWhiteSpace(created.Token));
        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var invitation = await dbContext.WorkspaceInvitations.SingleAsync(item => item.Id == created.Invitation.Id);
            Assert.NotEqual(created.Token, invitation.TokenHash);
            Assert.Equal(64, invitation.TokenHash.Length);
            Assert.Equal(WorkspaceMemberRole.Member, invitation.Role);
        }

        using var accept = await SendJsonWithBearerAsync(HttpMethod.Post, "/api/collaboration/invitations/accept", invited.Session.AccessToken, new AcceptWorkspaceInvitationRequest(created.Token));
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        await using var verifyScope = _fixture.Factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var membership = await verifyDb.WorkspaceMembers.SingleAsync(member => member.WorkspaceId == owner.Session.User.Workspace.Id && member.UserId == invited.Session.User.Id);
        Assert.Equal(WorkspaceMemberRole.Member, membership.Role);
    }

    /// <summary>Members cannot use owner-only collaboration admin endpoints.</summary>
    [Fact]
    public async Task CollaborationAdmin_WithMember_ReturnsForbidden()
    {
        var (owner, invited, _) = await InviteAcceptAndSwitchMemberAsync();

        using var response = await SendWithBearerAsync(HttpMethod.Get, "/api/collaboration/invitations", invited.Session.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(owner.Session.User.Workspace.Id, invited.Session.User.Workspace.Id);
        Assert.Equal("Member", invited.Session.User.Workspace.Role);
    }

    /// <summary>Wrong email and revoked invitations cannot be accepted.</summary>
    [Fact]
    public async Task Invitation_AcceptWithWrongEmailOrRevokedToken_ReturnsBadRequest()
    {
        var owner = await RegisterAsync("owner");
        var invited = await RegisterAsync("invited");
        var other = await RegisterAsync("other");
        var wrongEmailInvitation = await CreateInvitationAsync(owner.Session.AccessToken, invited.Session.User.Email);

        using var wrongEmail = await SendJsonWithBearerAsync(HttpMethod.Post, "/api/collaboration/invitations/accept", other.Session.AccessToken, new AcceptWorkspaceInvitationRequest(wrongEmailInvitation.Token));
        Assert.Equal(HttpStatusCode.BadRequest, wrongEmail.StatusCode);

        var revokedInvitation = await CreateInvitationAsync(owner.Session.AccessToken, other.Session.User.Email);
        using var revoke = await SendWithBearerAsync(HttpMethod.Post, $"/api/collaboration/invitations/{revokedInvitation.Invitation.Id}/revoke", owner.Session.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        using var revokedAccept = await SendJsonWithBearerAsync(HttpMethod.Post, "/api/collaboration/invitations/accept", other.Session.AccessToken, new AcceptWorkspaceInvitationRequest(revokedInvitation.Token));
        Assert.Equal(HttpStatusCode.BadRequest, revokedAccept.StatusCode);
    }

    /// <summary>Workspace switch rotates the refresh token and persists the active workspace across refresh.</summary>
    [Fact]
    public async Task SwitchWorkspace_PersistsActiveWorkspaceAcrossRefresh()
    {
        var (owner, invited, switchCookie) = await InviteAcceptAndSwitchMemberAsync();

        using var refresh = await SendWithRefreshCookieAsync(HttpMethod.Post, "/api/auth/refresh", switchCookie);
        var refreshedSession = await ReadSessionAsync(refresh);

        Assert.Equal(owner.Session.User.Workspace.Id, refreshedSession.User.Workspace.Id);
        Assert.Equal("Member", refreshedSession.User.Workspace.Role);
        Assert.Contains(refreshedSession.User.Workspaces, workspace => workspace.Id == invited.OriginalWorkspaceId);
    }

    /// <summary>Members can use existing operational endpoints in an invited workspace.</summary>
    [Fact]
    public async Task OperationalEndpoint_WithMemberWorkspace_ReturnsSuccess()
    {
        var (owner, invited, _) = await InviteAcceptAndSwitchMemberAsync();
        var suffix = Guid.NewGuid().ToString("N");

        using var create = await SendJsonWithBearerAsync(HttpMethod.Post, "/api/products", invited.Session.AccessToken, new CreateProductRequest($"Member Product {suffix}", $"MEM-{suffix}"));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var product = await create.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.NotNull(product);
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await dbContext.Products.AnyAsync(item => item.WorkspaceId == owner.Session.User.Workspace.Id && item.Id == product.Id));
    }

    /// <summary>A forged active workspace claim without SQL membership fails closed.</summary>
    [Fact]
    public async Task OperationalEndpoint_WithStaleWorkspaceClaim_ReturnsForbidden()
    {
        var user = await RegisterAsync("forged");
        var forgedToken = CreateAccessToken(user.Session.User.Id, user.Session.User.Email, user.Session.User.DisplayName, Guid.NewGuid(), "Member");

        using var response = await SendWithBearerAsync(HttpMethod.Get, "/api/products", forgedToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Accepting an already-accepted invitation is rejected (second accept fails).</summary>
    [Fact]
    public async Task Invitation_AcceptTwice_SecondAcceptRejected()
    {
        var owner = await RegisterAsync("owner");
        var invited = await RegisterAsync("invited");
        var created = await CreateInvitationAsync(owner.Session.AccessToken, invited.Session.User.Email);

        using var first = await SendJsonWithBearerAsync(HttpMethod.Post, "/api/collaboration/invitations/accept", invited.Session.AccessToken, new AcceptWorkspaceInvitationRequest(created.Token));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await SendJsonWithBearerAsync(HttpMethod.Post, "/api/collaboration/invitations/accept", invited.Session.AccessToken, new AcceptWorkspaceInvitationRequest(created.Token));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    /// <summary>Creating an invitation for a non-existent email is rejected.</summary>
    [Fact]
    public async Task Invitation_CreateForNonExistentEmail_ReturnsBadRequest()
    {
        var owner = await RegisterAsync("owner");

        using var response = await SendJsonWithBearerAsync(HttpMethod.Post, "/api/collaboration/invitations", owner.Session.AccessToken, new CreateWorkspaceInvitationRequest("nobody@example.test"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Creating an invitation for an already-member user is a conflict.</summary>
    [Fact]
    public async Task Invitation_CreateForExistingMember_ReturnsConflict()
    {
        var (owner, invited, _) = await InviteAcceptAndSwitchMemberAsync();

        using var response = await SendJsonWithBearerAsync(HttpMethod.Post, "/api/collaboration/invitations", owner.Session.AccessToken, new CreateWorkspaceInvitationRequest(invited.Session.User.Email));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>Creating a duplicate pending invitation is a conflict (unique index).</summary>
    [Fact]
    public async Task Invitation_CreateDuplicatePending_ReturnsConflict()
    {
        var owner = await RegisterAsync("owner");
        var invited = await RegisterAsync("invited");
        await CreateInvitationAsync(owner.Session.AccessToken, invited.Session.User.Email);

        using var response = await SendJsonWithBearerAsync(HttpMethod.Post, "/api/collaboration/invitations", owner.Session.AccessToken, new CreateWorkspaceInvitationRequest(invited.Session.User.Email));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>Switching into a workspace the user is not a member of is forbidden.</summary>
    [Fact]
    public async Task SwitchWorkspace_ToNonMemberWorkspace_ReturnsForbidden()
    {
        var owner = await RegisterAsync("owner");
        var stranger = await RegisterAsync("stranger");

        using var response = await SendJsonWithBearerAndCookieAsync(HttpMethod.Post, "/api/auth/workspace/switch", stranger.Session.AccessToken, stranger.RefreshCookie, new { workspaceId = owner.Session.User.Workspace.Id });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>GET /api/auth/me with a forged/stale workspace claim returns 401.</summary>
    [Fact]
    public async Task Me_WithStaleWorkspaceClaim_ReturnsUnauthorized()
    {
        var user = await RegisterAsync("stale");
        var forgedToken = CreateAccessToken(user.Session.User.Id, user.Session.User.Email, user.Session.User.DisplayName, Guid.NewGuid(), "Member");

        using var response = await SendWithBearerAsync(HttpMethod.Get, "/api/auth/me", forgedToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Owner can list members and invitations in the active workspace (200).</summary>
    [Fact]
    public async Task CollaborationAdmin_WithOwner_ReturnsMembersAndInvitations()
    {
        var owner = await RegisterAsync("owner");
        var invited = await RegisterAsync("invited");
        await CreateInvitationAsync(owner.Session.AccessToken, invited.Session.User.Email);

        using var members = await SendWithBearerAsync(HttpMethod.Get, "/api/collaboration/members", owner.Session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, members.StatusCode);
        var memberList = await members.Content.ReadFromJsonAsync<IReadOnlyCollection<WorkspaceMemberResponse>>(JsonOptions);
        Assert.NotNull(memberList);

        using var invitations = await SendWithBearerAsync(HttpMethod.Get, "/api/collaboration/invitations", owner.Session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, invitations.StatusCode);
        var invitationList = await invitations.Content.ReadFromJsonAsync<IReadOnlyCollection<WorkspaceInvitationResponse>>(JsonOptions);
        Assert.NotNull(invitationList);
        Assert.Contains(invitationList, item => string.Equals(item.Email, invited.Session.User.Email, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<(RegisteredUser Owner, RegisteredUser Invited, string SwitchCookie)> InviteAcceptAndSwitchMemberAsync()
    {
        var owner = await RegisterAsync("owner");
        var invited = await RegisterAsync("invited");
        var originalWorkspaceId = invited.Session.User.Workspace.Id;
        var invitation = await CreateInvitationAsync(owner.Session.AccessToken, invited.Session.User.Email);
        using var accept = await SendJsonWithBearerAsync(HttpMethod.Post, "/api/collaboration/invitations/accept", invited.Session.AccessToken, new AcceptWorkspaceInvitationRequest(invitation.Token));
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        using var switchResponse = await SendJsonWithBearerAndCookieAsync(HttpMethod.Post, "/api/auth/workspace/switch", invited.Session.AccessToken, invited.RefreshCookie, new { workspaceId = owner.Session.User.Workspace.Id });
        var switchedSession = await ReadSessionAsync(switchResponse);
        var switchCookie = GetRefreshCookieValue(switchResponse);
        return (owner, invited with { Session = switchedSession, RefreshCookie = switchCookie, OriginalWorkspaceId = originalWorkspaceId }, switchCookie);
    }

    private async Task<RegisteredUser> RegisterAsync(string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterUserCommand($"{prefix} {suffix}", $"{prefix}-{suffix}@example.test", "Password!12345"));
        var session = await ReadSessionAsync(response);
        return new RegisteredUser(session, GetRefreshCookieValue(response), session.User.Workspace.Id);
    }

    private async Task<CreatedWorkspaceInvitationResponse> CreateInvitationAsync(string accessToken, string email)
    {
        using var response = await SendJsonWithBearerAsync(HttpMethod.Post, "/api/collaboration/invitations", accessToken, new CreateWorkspaceInvitationRequest(email));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invitation = await response.Content.ReadFromJsonAsync<CreatedWorkspaceInvitationResponse>(JsonOptions);
        return Assert.IsType<CreatedWorkspaceInvitationResponse>(invitation);
    }

    private async Task<HttpResponseMessage> SendWithBearerAsync(HttpMethod method, string path, string accessToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendJsonWithBearerAsync<T>(HttpMethod method, string path, string accessToken, T body)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendJsonWithBearerAndCookieAsync<T>(HttpMethod method, string path, string accessToken, string refreshCookie, T body)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Cookie", $"{RefreshCookieName}={refreshCookie}");
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendWithRefreshCookieAsync(HttpMethod method, string path, string refreshCookie)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", $"{RefreshCookieName}={refreshCookie}");
        return await _client.SendAsync(request);
    }

    private static async Task<AuthenticationResponse> ReadSessionAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<AuthenticationResponse>(JsonOptions);
        return Assert.IsType<AuthenticationResponse>(session);
    }

    private static string GetRefreshCookieValue(HttpResponseMessage response)
    {
        var header = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        var cookie = header.Split(';', 2)[0].Split('=', 2);
        Assert.Equal(RefreshCookieName, cookie[0]);
        return Uri.UnescapeDataString(cookie[1]);
    }

    private static string CreateAccessToken(Guid userId, string email, string displayName, Guid workspaceId, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-signing-key-that-is-at-least-thirty-two-bytes-long"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            "InventoryFlow.Test",
            "InventoryFlow.Test.Web",
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim("display_name", displayName),
                new Claim(JwtAccessTokenIssuer.WorkspaceIdClaimType, workspaceId.ToString()),
                new Claim(JwtAccessTokenIssuer.WorkspaceRoleClaimType, role)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record RegisteredUser(AuthenticationResponse Session, string RefreshCookie, Guid OriginalWorkspaceId);
}
