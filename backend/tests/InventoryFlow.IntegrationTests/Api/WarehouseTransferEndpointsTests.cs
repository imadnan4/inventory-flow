using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InventoryFlow.Application.Features.Authentication;
using InventoryFlow.Application.Features.Products;
using InventoryFlow.Application.Features.Purchases;
using InventoryFlow.Application.Features.Suppliers;
using InventoryFlow.Application.Features.Transfers;
using InventoryFlow.Application.Features.Warehouses;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryFlow.IntegrationTests.Api;

public sealed class WarehouseTransferEndpointsTests : IClassFixture<AuthenticatedApiFixture>
{
    private readonly AuthenticatedApiFixture _fixture;
    private readonly HttpClient _client;

    public WarehouseTransferEndpointsTests(AuthenticatedApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    [Fact]
    public async Task Transfers_WithoutAccessToken_ReturnUnauthorized()
    {
        using var post = await _client.PostAsJsonAsync("/api/transfers",
            new RecordWarehouseTransferRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1m, "unauthenticated"));
        using var get = await _client.GetAsync("/api/transfers");

        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
    }

    [Fact]
    public async Task Transfer_IsAtomicConservingAndDurablyReplayed()
    {
        var session = await RegisterAsync();
        var sources = await CreateSourcesAsync(session, 5m);
        var request = new RecordWarehouseTransferRequest(sources.Source.Id, sources.Destination.Id, sources.Product.Id, 2m,
            $"transfer-{Guid.NewGuid():N}");

        using var first = await SendAsync(HttpMethod.Post, "/api/transfers", session.AccessToken, request);
        using var replay = await SendAsync(HttpMethod.Post, "/api/transfers", session.AccessToken, request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        var posted = (await first.Content.ReadFromJsonAsync<WarehouseTransferResponse>())!;
        Assert.Equal(posted.Id, (await replay.Content.ReadFromJsonAsync<WarehouseTransferResponse>())!.Id);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var transfer = await db.WarehouseTransfers.SingleAsync(item => item.Id == posted.Id);
        var movements = await db.InventoryMovements.Where(item => item.Id == transfer.SourceInventoryMovementId ||
            item.Id == transfer.DestinationInventoryMovementId).ToListAsync();
        var balances = await db.InventoryBalances.Where(item => item.WorkspaceId == session.User.Workspace.Id &&
            item.ProductId == sources.Product.Id).ToListAsync();

        Assert.Equal(2, movements.Count);
        Assert.Contains(movements, item => item.Id == posted.SourceInventoryMovementId && item.Type == InventoryMovementType.Issue && item.Quantity == 2m);
        Assert.Contains(movements, item => item.Id == posted.DestinationInventoryMovementId && item.Type == InventoryMovementType.Receipt && item.Quantity == 2m);
        Assert.Equal(movements[0].OccurredAtUtc, movements[1].OccurredAtUtc);
        Assert.Equal(3m, balances.Single(item => item.WarehouseId == sources.Source.Id).Quantity);
        Assert.Equal(2m, balances.Single(item => item.WarehouseId == sources.Destination.Id).Quantity);
        Assert.Equal(5m, balances.Sum(item => item.Quantity));
        Assert.Single(await db.WarehouseTransfers.Where(item => item.WorkspaceId == session.User.Workspace.Id).ToListAsync());
    }

    [Fact]
    public async Task OppositeDirectionTransfersUseCompletePairsAndConserveInventory()
    {
        var session = await RegisterAsync();
        var sources = await CreateSourcesAsync(session, 20m);
        var reverseSupplier = await PostAsync<SupplierResponse>("/api/suppliers", session.AccessToken,
            new CreateSupplierRequest($"Supplier {Guid.NewGuid():N}"));
        await PostAsync<PurchaseReceiptResponse>("/api/purchases/receipts", session.AccessToken,
            new RecordPurchaseReceiptRequest(reverseSupplier.Id, sources.Destination.Id, sources.Product.Id, 20m,
                $"receipt-{Guid.NewGuid():N}"));

        var forward = SendAsync(HttpMethod.Post, "/api/transfers", session.AccessToken,
            new RecordWarehouseTransferRequest(sources.Source.Id, sources.Destination.Id, sources.Product.Id, 3m, $"forward-{Guid.NewGuid():N}"));
        var reverse = SendAsync(HttpMethod.Post, "/api/transfers", session.AccessToken,
            new RecordWarehouseTransferRequest(sources.Destination.Id, sources.Source.Id, sources.Product.Id, 4m, $"reverse-{Guid.NewGuid():N}"));
        var responses = await Task.WhenAll(forward, reverse);
        using var first = responses[0];
        using var second = responses[1];
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var transfers = await db.WarehouseTransfers.Where(item => item.WorkspaceId == session.User.Workspace.Id).ToListAsync();
        var balances = await db.InventoryBalances.Where(item => item.WorkspaceId == session.User.Workspace.Id &&
            item.ProductId == sources.Product.Id).ToListAsync();
        Assert.Equal(2, transfers.Count);
        Assert.Equal(6, await db.InventoryMovements.CountAsync(item => item.WorkspaceId == session.User.Workspace.Id));
        Assert.Equal(40m, balances.Sum(item => item.Quantity));
        Assert.All(balances, balance => Assert.True(balance.Quantity >= 0m));
    }

    [Fact]
    public async Task Transfer_WithInsufficientInventoryLeavesNoPairOrDestinationBalance()
    {
        var session = await RegisterAsync();
        var sources = await CreateSourcesAsync(session, 1m);
        using var response = await SendAsync(HttpMethod.Post, "/api/transfers", session.AccessToken,
            new RecordWarehouseTransferRequest(sources.Source.Id, sources.Destination.Id, sources.Product.Id, 1.0001m,
                $"overdraft-{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.WarehouseTransfers.Where(item => item.WorkspaceId == session.User.Workspace.Id).ToListAsync());
        Assert.Empty(await db.InventoryBalances.Where(item => item.WorkspaceId == session.User.Workspace.Id &&
            item.WarehouseId == sources.Destination.Id && item.ProductId == sources.Product.Id).ToListAsync());
        Assert.Equal(1m, await db.InventoryBalances.Where(item => item.WorkspaceId == session.User.Workspace.Id &&
            item.WarehouseId == sources.Source.Id && item.ProductId == sources.Product.Id).Select(item => item.Quantity).SingleAsync());
    }

    [Fact]
    public async Task Transfer_ConcurrentSameKeyReplayCreatesOneTransferAndPair()
    {
        var session = await RegisterAsync();
        var sources = await CreateSourcesAsync(session, 5m);
        var request = new RecordWarehouseTransferRequest(sources.Source.Id, sources.Destination.Id, sources.Product.Id, 2m,
            $"concurrent-replay-{Guid.NewGuid():N}");

        var responses = await Task.WhenAll(
            SendAsync(HttpMethod.Post, "/api/transfers", session.AccessToken, request),
            SendAsync(HttpMethod.Post, "/api/transfers", session.AccessToken, request));
        using var first = responses[0];
        using var second = responses[1];
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal((await first.Content.ReadFromJsonAsync<WarehouseTransferResponse>())!.Id,
            (await second.Content.ReadFromJsonAsync<WarehouseTransferResponse>())!.Id);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var transfer = await db.WarehouseTransfers.SingleAsync(item => item.WorkspaceId == session.User.Workspace.Id);
        Assert.Equal(2, await db.InventoryMovements.CountAsync(item => item.Id == transfer.SourceInventoryMovementId ||
            item.Id == transfer.DestinationInventoryMovementId));
        Assert.Equal(3m, await db.InventoryBalances.Where(item => item.WorkspaceId == session.User.Workspace.Id &&
            item.WarehouseId == sources.Source.Id && item.ProductId == sources.Product.Id).Select(item => item.Quantity).SingleAsync());
        Assert.Equal(2m, await db.InventoryBalances.Where(item => item.WorkspaceId == session.User.Workspace.Id &&
            item.WarehouseId == sources.Destination.Id && item.ProductId == sources.Product.Id).Select(item => item.Quantity).SingleAsync());
    }

    [Fact]
    public async Task Transfers_WithCrossTenantIdsReturnNotFoundWithoutSideEffects()
    {
        var firstSession = await RegisterAsync();
        var secondSession = await RegisterAsync();
        var firstSources = await CreateSourcesAsync(firstSession);
        var secondSources = await CreateSourcesAsync(secondSession);

        using var foreignSource = await SendAsync(HttpMethod.Post, "/api/transfers", secondSession.AccessToken,
            new RecordWarehouseTransferRequest(firstSources.Source.Id, secondSources.Destination.Id, secondSources.Product.Id, 1m,
                $"foreign-source-{Guid.NewGuid():N}"));
        using var foreignDestination = await SendAsync(HttpMethod.Post, "/api/transfers", secondSession.AccessToken,
            new RecordWarehouseTransferRequest(secondSources.Source.Id, firstSources.Destination.Id, secondSources.Product.Id, 1m,
                $"foreign-destination-{Guid.NewGuid():N}"));
        using var foreignProduct = await SendAsync(HttpMethod.Post, "/api/transfers", secondSession.AccessToken,
            new RecordWarehouseTransferRequest(secondSources.Source.Id, secondSources.Destination.Id, firstSources.Product.Id, 1m,
                $"foreign-product-{Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.NotFound, foreignSource.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignDestination.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignProduct.StatusCode);
        await AssertNoTransferSideEffectsAsync(secondSession.User.Workspace.Id);
    }

    [Fact]
    public async Task Transfers_WithArchivedProductSourceOrDestinationReturnNotFoundWithoutSideEffects()
    {
        var session = await RegisterAsync();
        var archivedProductSources = await CreateSourcesAsync(session);
        var archivedSourceSources = await CreateSourcesAsync(session);
        var archivedDestinationSources = await CreateSourcesAsync(session);

        using var archiveProduct = await SendAsync(HttpMethod.Delete, $"/api/products/{archivedProductSources.Product.Id}", session.AccessToken);
        using var archiveSource = await SendAsync(HttpMethod.Delete, $"/api/warehouses/{archivedSourceSources.Source.Id}", session.AccessToken);
        using var archiveDestination = await SendAsync(HttpMethod.Delete, $"/api/warehouses/{archivedDestinationSources.Destination.Id}", session.AccessToken);
        using var archivedProduct = await SendAsync(HttpMethod.Post, "/api/transfers", session.AccessToken,
            new RecordWarehouseTransferRequest(archivedProductSources.Source.Id, archivedProductSources.Destination.Id,
                archivedProductSources.Product.Id, 1m, $"archived-product-{Guid.NewGuid():N}"));
        using var archivedSource = await SendAsync(HttpMethod.Post, "/api/transfers", session.AccessToken,
            new RecordWarehouseTransferRequest(archivedSourceSources.Source.Id, archivedSourceSources.Destination.Id,
                archivedSourceSources.Product.Id, 1m, $"archived-source-{Guid.NewGuid():N}"));
        using var archivedDestination = await SendAsync(HttpMethod.Post, "/api/transfers", session.AccessToken,
            new RecordWarehouseTransferRequest(archivedDestinationSources.Source.Id, archivedDestinationSources.Destination.Id,
                archivedDestinationSources.Product.Id, 1m, $"archived-destination-{Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.NoContent, archiveProduct.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, archiveSource.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, archiveDestination.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, archivedProduct.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, archivedSource.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, archivedDestination.StatusCode);
        await AssertNoTransferSideEffectsAsync(session.User.Workspace.Id);
    }

    private async Task AssertNoTransferSideEffectsAsync(Guid workspaceId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.WarehouseTransfers.Where(item => item.WorkspaceId == workspaceId).ToListAsync());
        Assert.Empty(await db.InventoryMovements.Where(item => item.WorkspaceId == workspaceId).ToListAsync());
        Assert.Empty(await db.InventoryBalances.Where(item => item.WorkspaceId == workspaceId).ToListAsync());
    }

    private async Task<Sources> CreateSourcesAsync(AuthenticationResponse session, decimal? quantity = null)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var supplier = await PostAsync<SupplierResponse>("/api/suppliers", session.AccessToken, new CreateSupplierRequest($"Supplier {suffix}"));
        var source = await PostAsync<WarehouseResponse>("/api/warehouses", session.AccessToken, new CreateWarehouseRequest($"Source {suffix}"));
        var destination = await PostAsync<WarehouseResponse>("/api/warehouses", session.AccessToken, new CreateWarehouseRequest($"Destination {suffix}"));
        var product = await PostAsync<ProductResponse>("/api/products", session.AccessToken, new CreateProductRequest($"Product {suffix}", $"SKU-{suffix}"));
        if (quantity.HasValue)
            await PostAsync<PurchaseReceiptResponse>("/api/purchases/receipts", session.AccessToken,
                new RecordPurchaseReceiptRequest(supplier.Id, source.Id, product.Id, quantity.Value, $"receipt-{suffix}"));
        return new Sources(source, destination, product);
    }

    private async Task<T> PostAsync<T>(string path, string token, object content)
    {
        using var response = await SendAsync(HttpMethod.Post, path, token, content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<AuthenticationResponse> RegisterAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserCommand($"User {suffix}",
            $"user-{suffix}@example.test", "Password!12345"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token, object? content = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (content is not null) request.Content = JsonContent.Create(content);
        return await _client.SendAsync(request);
    }

    private sealed record Sources(WarehouseResponse Source, WarehouseResponse Destination, ProductResponse Product);
}
