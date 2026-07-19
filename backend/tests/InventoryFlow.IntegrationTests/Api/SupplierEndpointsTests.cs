using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InventoryFlow.Application.Features.Authentication;
using InventoryFlow.Application.Features.Suppliers;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryFlow.IntegrationTests.Api;

/// <summary>Verifies workspace-scoped supplier API behavior against SQL Server.</summary>
public sealed class SupplierEndpointsTests : IClassFixture<AuthenticatedApiFixture>
{
    private readonly HttpClient _client;
    private readonly AuthenticatedApiFixture _fixture;

    /// <summary>Initializes the test client.</summary>
    public SupplierEndpointsTests(AuthenticatedApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    /// <summary>Rejects unauthenticated supplier requests.</summary>
    [Fact]
    public async Task List_WithoutAccessToken_ReturnsUnauthorized()
    {
        using var response = await _client.GetAsync("/api/suppliers");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Creates, lists, archives, and permanently reserves a normalized name through the database unique index.</summary>
    [Fact]
    public async Task SupplierLifecycle_UsesNormalizedNameAndKeepsArchivedNameReserved()
    {
        var session = await RegisterSessionAsync();
        var token = session.AccessToken;
        using var created = await SendAsync(HttpMethod.Post, "/api/suppliers", token, new CreateSupplierRequest("  Acme Supply  "));
        var supplier = await created.Content.ReadFromJsonAsync<SupplierResponse>();
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Acme Supply", supplier!.Name);
        using var listed = await SendAsync(HttpMethod.Get, "/api/suppliers", token);
        Assert.Single(await listed.Content.ReadFromJsonAsync<List<SupplierResponse>>() ?? []);
        using var archived = await SendAsync(HttpMethod.Delete, $"/api/suppliers/{supplier.Id}", token);
        using var archivedAgain = await SendAsync(HttpMethod.Delete, $"/api/suppliers/{supplier.Id}", token);
        using var duplicate = await SendAsync(HttpMethod.Post, "/api/suppliers", token, new CreateSupplierRequest(" acme supply "));
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, archivedAgain.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await dbContext.Suppliers.CountAsync(supplier => supplier.WorkspaceId == session.User.Workspace.Id));
    }

    /// <summary>Enforces workspace isolation while allowing the same name in different workspaces.</summary>
    [Fact]
    public async Task Suppliers_AreIsolatedByWorkspace()
    {
        var firstToken = await RegisterAsync();
        var secondToken = await RegisterAsync();
        using var firstCreate = await SendAsync(HttpMethod.Post, "/api/suppliers", firstToken, new CreateSupplierRequest("Shared"));
        var firstSupplier = await firstCreate.Content.ReadFromJsonAsync<SupplierResponse>();
        using var secondCreate = await SendAsync(HttpMethod.Post, "/api/suppliers", secondToken, new CreateSupplierRequest(" shared "));
        using var secondList = await SendAsync(HttpMethod.Get, "/api/suppliers", secondToken);
        using var crossArchive = await SendAsync(HttpMethod.Delete, $"/api/suppliers/{firstSupplier!.Id}", secondToken);
        Assert.Equal(HttpStatusCode.Created, firstCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondCreate.StatusCode);
        Assert.Single(await secondList.Content.ReadFromJsonAsync<List<SupplierResponse>>() ?? []);
        Assert.Equal(HttpStatusCode.NotFound, crossArchive.StatusCode);
    }

    /// <summary>Maps a same-workspace name race through the database unique index to one creation and one conflict.</summary>
    [Fact]
    public async Task Create_ConcurrentNormalizedNameRequests_CreateOneSupplierAndReturnOneConflict()
    {
        var session = await RegisterSessionAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var firstRequest = SendAsync(HttpMethod.Post, "/api/suppliers", session.AccessToken, new CreateSupplierRequest($"Supplier-{suffix}"));
        var secondRequest = SendAsync(HttpMethod.Post, "/api/suppliers", session.AccessToken, new CreateSupplierRequest($" supplier-{suffix.ToUpperInvariant()} "));

        var responses = await Task.WhenAll(firstRequest, secondRequest);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await dbContext.Suppliers.CountAsync(supplier => supplier.WorkspaceId == session.User.Workspace.Id));
    }

    private async Task<string> RegisterAsync() => (await RegisterSessionAsync()).AccessToken;

    private async Task<AuthenticationResponse> RegisterSessionAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserCommand($"User {suffix}", $"user-{suffix}@example.test", "Password!12345"));
        var session = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return session!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token, object? content = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (content is not null) request.Content = JsonContent.Create(content);
        return await _client.SendAsync(request);
    }
}
