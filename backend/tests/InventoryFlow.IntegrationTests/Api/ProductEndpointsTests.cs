using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InventoryFlow.Application.Features.Authentication;
using InventoryFlow.Application.Features.Products;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryFlow.IntegrationTests.Api;

/// <summary>Verifies workspace-scoped product API behavior against SQL Server.</summary>
public sealed class ProductEndpointsTests : IClassFixture<AuthenticatedApiFixture>
{
    private readonly HttpClient _client;
    private readonly AuthenticatedApiFixture _fixture;

    /// <summary>Initializes the test client.</summary>
    public ProductEndpointsTests(AuthenticatedApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    /// <summary>Creates, lists, archives, and permanently reserves a SKU through the database unique index.</summary>
    [Fact]
    public async Task ProductLifecycle_UsesCanonicalSkuAndKeepsArchivedSkuReserved()
    {
        var session = await RegisterSessionAsync();
        var token = session.AccessToken;
        using var created = await SendAsync(HttpMethod.Post, "/api/products", token, new CreateProductRequest("  Widget  ", " ab-1 "));
        var product = await created.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("AB-1", product!.Sku);
        using var listed = await SendAsync(HttpMethod.Get, "/api/products", token);
        Assert.Single(await listed.Content.ReadFromJsonAsync<List<ProductResponse>>() ?? []);
        using var archived = await SendAsync(HttpMethod.Delete, $"/api/products/{product.Id}", token);
        using var archivedAgain = await SendAsync(HttpMethod.Delete, $"/api/products/{product.Id}", token);
        using var duplicate = await SendAsync(HttpMethod.Post, "/api/products", token, new CreateProductRequest("Another", "AB-1"));
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, archivedAgain.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await dbContext.Products.CountAsync(product => product.WorkspaceId == session.User.Workspace.Id));
    }

    /// <summary>Enforces workspace isolation while allowing same SKU in different workspaces.</summary>
    [Fact]
    public async Task Products_AreIsolatedByWorkspace()
    {
        var firstToken = await RegisterAsync();
        var secondToken = await RegisterAsync();
        using var firstCreate = await SendAsync(HttpMethod.Post, "/api/products", firstToken, new CreateProductRequest("First", "SHARED"));
        var firstProduct = await firstCreate.Content.ReadFromJsonAsync<ProductResponse>();
        using var secondCreate = await SendAsync(HttpMethod.Post, "/api/products", secondToken, new CreateProductRequest("Second", "shared"));
        using var secondList = await SendAsync(HttpMethod.Get, "/api/products", secondToken);
        using var crossArchive = await SendAsync(HttpMethod.Delete, $"/api/products/{firstProduct!.Id}", secondToken);
        Assert.Equal(HttpStatusCode.Created, firstCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondCreate.StatusCode);
        Assert.Single(await secondList.Content.ReadFromJsonAsync<List<ProductResponse>>() ?? []);
        Assert.Equal(HttpStatusCode.NotFound, crossArchive.StatusCode);
    }

    /// <summary>Maps a same-workspace SKU race through the database unique index to one creation and one conflict.</summary>
    [Fact]
    public async Task Create_ConcurrentCanonicalSkuRequests_CreateOneProductAndReturnOneConflict()
    {
        // Arrange
        var session = await RegisterSessionAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var firstRequest = SendAsync(HttpMethod.Post, "/api/products", session.AccessToken, new CreateProductRequest("First", $"sku-{suffix}"));
        var secondRequest = SendAsync(HttpMethod.Post, "/api/products", session.AccessToken, new CreateProductRequest("Second", $" SKU-{suffix.ToUpperInvariant()} "));

        // Act
        var responses = await Task.WhenAll(firstRequest, secondRequest);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        // Assert
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await dbContext.Products.CountAsync(product => product.WorkspaceId == session.User.Workspace.Id));
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
