using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InventoryFlow.Application.Features.Authentication;
using InventoryFlow.Application.Features.Warehouses;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryFlow.IntegrationTests.Api;

/// <summary>Verifies workspace-scoped warehouse API behavior against SQL Server.</summary>
public sealed class WarehouseEndpointsTests : IClassFixture<AuthenticatedApiFixture>
{
    private readonly HttpClient _client;
    private readonly AuthenticatedApiFixture _fixture;

    /// <summary>Initializes the test client.</summary>
    public WarehouseEndpointsTests(AuthenticatedApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    /// <summary>Rejects unauthenticated warehouse requests.</summary>
    [Fact]
    public async Task List_WithoutAccessToken_ReturnsUnauthorized()
    {
        using var response = await _client.GetAsync("/api/warehouses");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Creates, lists, archives, and permanently reserves a name through the database unique index.</summary>
    [Fact]
    public async Task WarehouseLifecycle_UsesCanonicalNameAndKeepsArchivedNameReserved()
    {
        var session = await RegisterSessionAsync();
        var token = session.AccessToken;
        using var created = await SendAsync(HttpMethod.Post, "/api/warehouses", token, new CreateWarehouseRequest("  Central Depot  "));
        var warehouse = await created.Content.ReadFromJsonAsync<WarehouseResponse>();
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Central Depot", warehouse!.Name);
        using var listed = await SendAsync(HttpMethod.Get, "/api/warehouses", token);
        Assert.Single(await listed.Content.ReadFromJsonAsync<List<WarehouseResponse>>() ?? []);
        using var archived = await SendAsync(HttpMethod.Delete, $"/api/warehouses/{warehouse.Id}", token);
        using var archivedAgain = await SendAsync(HttpMethod.Delete, $"/api/warehouses/{warehouse.Id}", token);
        using var duplicate = await SendAsync(HttpMethod.Post, "/api/warehouses", token, new CreateWarehouseRequest("Central Depot"));
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, archivedAgain.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await dbContext.Warehouses.CountAsync(warehouse => warehouse.WorkspaceId == session.User.Workspace.Id));
    }

    /// <summary>Enforces workspace isolation while allowing same name in different workspaces.</summary>
    [Fact]
    public async Task Warehouses_AreIsolatedByWorkspace()
    {
        var firstToken = await RegisterAsync();
        var secondToken = await RegisterAsync();
        using var firstCreate = await SendAsync(HttpMethod.Post, "/api/warehouses", firstToken, new CreateWarehouseRequest("Shared"));
        var firstWarehouse = await firstCreate.Content.ReadFromJsonAsync<WarehouseResponse>();
        using var secondCreate = await SendAsync(HttpMethod.Post, "/api/warehouses", secondToken, new CreateWarehouseRequest(" shared "));
        using var secondList = await SendAsync(HttpMethod.Get, "/api/warehouses", secondToken);
        using var crossArchive = await SendAsync(HttpMethod.Delete, $"/api/warehouses/{firstWarehouse!.Id}", secondToken);
        Assert.Equal(HttpStatusCode.Created, firstCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondCreate.StatusCode);
        Assert.Single(await secondList.Content.ReadFromJsonAsync<List<WarehouseResponse>>() ?? []);
        Assert.Equal(HttpStatusCode.NotFound, crossArchive.StatusCode);
    }

    /// <summary>Maps a same-workspace name race through the database unique index to one creation and one conflict.</summary>
    [Fact]
    public async Task Create_ConcurrentCanonicalNameRequests_CreateOneWarehouseAndReturnOneConflict()
    {
        // Arrange
        var session = await RegisterSessionAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var firstRequest = SendAsync(HttpMethod.Post, "/api/warehouses", session.AccessToken, new CreateWarehouseRequest($"Depot-{suffix}"));
        var secondRequest = SendAsync(HttpMethod.Post, "/api/warehouses", session.AccessToken, new CreateWarehouseRequest($" depot-{suffix.ToUpperInvariant()} "));

        // Act
        var responses = await Task.WhenAll(firstRequest, secondRequest);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        // Assert
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await dbContext.Warehouses.CountAsync(warehouse => warehouse.WorkspaceId == session.User.Workspace.Id));
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
