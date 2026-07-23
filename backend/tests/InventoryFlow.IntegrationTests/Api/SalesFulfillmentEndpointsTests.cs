using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InventoryFlow.Application.Features.Authentication;
using InventoryFlow.Application.Features.Products;
using InventoryFlow.Application.Features.Purchases;
using InventoryFlow.Application.Features.Sales;
using InventoryFlow.Application.Features.Suppliers;
using InventoryFlow.Application.Features.Warehouses;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryFlow.IntegrationTests.Api;

public sealed class SalesFulfillmentEndpointsTests : IClassFixture<AuthenticatedApiFixture>
{
    private readonly AuthenticatedApiFixture _fixture;
    private readonly HttpClient _client;

    public SalesFulfillmentEndpointsTests(AuthenticatedApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    [Fact]
    public async Task Fulfillments_WithoutAccessToken_ReturnUnauthorized()
    {
        using var post = await _client.PostAsJsonAsync("/api/sales/fulfillments",
            new RecordSalesFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), 1m, "unauthenticated"));
        using var get = await _client.GetAsync("/api/sales/fulfillments");

        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
    }

    [Fact]
    public async Task Fulfillments_CrossWorkspaceSourcesReturnNotFoundAndHistoryIsIsolated()
    {
        var firstSession = await RegisterAsync();
        var secondSession = await RegisterAsync();
        var firstSources = await CreateFulfillmentSourcesAsync(firstSession, 2m);
        var secondSources = await CreateFulfillmentSourcesAsync(secondSession, 2m);
        await PostAsync<SalesFulfillmentResponse>("/api/sales/fulfillments", firstSession.AccessToken,
            new RecordSalesFulfillmentRequest(firstSources.Warehouse.Id, firstSources.Product.Id, 1m, $"first-{Guid.NewGuid():N}"));

        using var foreignWarehouse = await SendAsync(HttpMethod.Post, "/api/sales/fulfillments", secondSession.AccessToken,
            new RecordSalesFulfillmentRequest(firstSources.Warehouse.Id, secondSources.Product.Id, 1m, $"warehouse-{Guid.NewGuid():N}"));
        using var foreignProduct = await SendAsync(HttpMethod.Post, "/api/sales/fulfillments", secondSession.AccessToken,
            new RecordSalesFulfillmentRequest(secondSources.Warehouse.Id, firstSources.Product.Id, 1m, $"product-{Guid.NewGuid():N}"));
        using var firstHistory = await SendAsync(HttpMethod.Get, "/api/sales/fulfillments", firstSession.AccessToken);
        using var secondHistory = await SendAsync(HttpMethod.Get, "/api/sales/fulfillments", secondSession.AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, foreignWarehouse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignProduct.StatusCode);
        Assert.Single((await firstHistory.Content.ReadFromJsonAsync<List<SalesFulfillmentResponse>>())!);
        Assert.Empty((await secondHistory.Content.ReadFromJsonAsync<List<SalesFulfillmentResponse>>())!);
    }

    [Fact]
    public async Task Fulfillments_WithArchivedSourcesReturnNotFoundWithoutSideEffects()
    {
        var session = await RegisterAsync();
        var archivedProductSources = await CreateFulfillmentSourcesAsync(session);
        var beforeProduct = await SnapshotAsync(session.User.Workspace.Id);
        using var archiveProduct = await SendAsync(HttpMethod.Delete, $"/api/products/{archivedProductSources.Product.Id}", session.AccessToken);
        using var productFulfillment = await SendAsync(HttpMethod.Post, "/api/sales/fulfillments", session.AccessToken,
            CreateFulfillmentRequest(archivedProductSources, $"archived-product-{Guid.NewGuid():N}"));

        var archivedWarehouseSources = await CreateFulfillmentSourcesAsync(session);
        var beforeWarehouse = await SnapshotAsync(session.User.Workspace.Id);
        using var archiveWarehouse = await SendAsync(HttpMethod.Delete, $"/api/warehouses/{archivedWarehouseSources.Warehouse.Id}", session.AccessToken);
        using var warehouseFulfillment = await SendAsync(HttpMethod.Post, "/api/sales/fulfillments", session.AccessToken,
            CreateFulfillmentRequest(archivedWarehouseSources, $"archived-warehouse-{Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.NoContent, archiveProduct.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, productFulfillment.StatusCode);
        Assert.Equal(beforeProduct, await SnapshotAsync(session.User.Workspace.Id));
        Assert.Equal(HttpStatusCode.NoContent, archiveWarehouse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, warehouseFulfillment.StatusCode);
        Assert.Equal(beforeWarehouse, await SnapshotAsync(session.User.Workspace.Id));
    }

    [Fact]
    public async Task Fulfillment_IsAtomicAndReplayReturnsTheOriginalDocument()
    {
        var session = await RegisterAsync();
        var sources = await CreateFulfillmentSourcesAsync(session, 4m);
        var request = CreateFulfillmentRequest(sources, $"fulfillment-{Guid.NewGuid():N}", 2.5m);

        using var first = await SendAsync(HttpMethod.Post, "/api/sales/fulfillments", session.AccessToken, request);
        using var replay = await SendAsync(HttpMethod.Post, "/api/sales/fulfillments", session.AccessToken, request);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        var posted = (await first.Content.ReadFromJsonAsync<SalesFulfillmentResponse>())!;
        var replayed = (await replay.Content.ReadFromJsonAsync<SalesFulfillmentResponse>())!;
        Assert.Equal(posted.Id, replayed.Id);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var fulfillment = await db.SalesFulfillments.SingleAsync(item => item.Id == posted.Id);
        var movement = await db.InventoryMovements.SingleAsync(item => item.Id == fulfillment.InventoryMovementId);
        var balance = await db.InventoryBalances.SingleAsync(item => item.WorkspaceId == session.User.Workspace.Id &&
            item.WarehouseId == sources.Warehouse.Id && item.ProductId == sources.Product.Id);
        Assert.Equal(InventoryMovementType.Issue, movement.Type);
        Assert.Equal(1.5m, balance.Quantity);
        Assert.Equal(1, await db.SalesFulfillments.CountAsync(item => item.WorkspaceId == session.User.Workspace.Id));
        Assert.Equal(2, await db.InventoryMovements.CountAsync(item => item.WorkspaceId == session.User.Workspace.Id));
    }

    [Fact]
    public async Task Fulfillment_WithInsufficientInventoryReturnsConflictWithoutSideEffects()
    {
        var session = await RegisterAsync();
        var sources = await CreateFulfillmentSourcesAsync(session, 1m);
        var before = await SnapshotAsync(session.User.Workspace.Id);

        using var response = await SendAsync(HttpMethod.Post, "/api/sales/fulfillments", session.AccessToken,
            CreateFulfillmentRequest(sources, $"overdraft-{Guid.NewGuid():N}", 1.0001m));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(before, await SnapshotAsync(session.User.Workspace.Id));
    }

    [Fact]
    public async Task FulfillmentHistory_IsOrderedNewestFirst()
    {
        var session = await RegisterAsync();
        var sources = await CreateFulfillmentSourcesAsync(session, 3m);
        var first = await PostAsync<SalesFulfillmentResponse>("/api/sales/fulfillments", session.AccessToken,
            CreateFulfillmentRequest(sources, $"first-{Guid.NewGuid():N}", 1m));
        await Task.Delay(TimeSpan.FromMilliseconds(10));
        var second = await PostAsync<SalesFulfillmentResponse>("/api/sales/fulfillments", session.AccessToken,
            CreateFulfillmentRequest(sources, $"second-{Guid.NewGuid():N}", 1m));
        using var response = await SendAsync(HttpMethod.Get, "/api/sales/fulfillments", session.AccessToken);

        var history = (await response.Content.ReadFromJsonAsync<List<SalesFulfillmentResponse>>())!;
        Assert.Collection(history, item => Assert.Equal(second.Id, item.Id), item => Assert.Equal(first.Id, item.Id));
        Assert.True(history[0].FulfilledAtUtc > history[1].FulfilledAtUtc);
    }

    [Fact]
    public async Task Fulfillment_ConcurrentReplayCreatesOneDocumentAndIssue()
    {
        var session = await RegisterAsync();
        var sources = await CreateFulfillmentSourcesAsync(session, 2m);
        var request = CreateFulfillmentRequest(sources, $"fulfillment-{Guid.NewGuid():N}");

        var responses = await Task.WhenAll(
            SendAsync(HttpMethod.Post, "/api/sales/fulfillments", session.AccessToken, request),
            SendAsync(HttpMethod.Post, "/api/sales/fulfillments", session.AccessToken, request));
        using var first = responses[0];
        using var second = responses[1];
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.SalesFulfillments.CountAsync(item => item.WorkspaceId == session.User.Workspace.Id));
        Assert.Equal(2, await db.InventoryMovements.CountAsync(item => item.WorkspaceId == session.User.Workspace.Id));
        Assert.Equal(1m, await db.InventoryBalances.Where(item => item.WorkspaceId == session.User.Workspace.Id)
            .Select(item => item.Quantity).SingleAsync());
    }

    private async Task<FulfillmentSources> CreateFulfillmentSourcesAsync(AuthenticationResponse session,
        decimal? receivedQuantity = null)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var supplier = await PostAsync<SupplierResponse>("/api/suppliers", session.AccessToken, new CreateSupplierRequest($"Supplier {suffix}"));
        var warehouse = await PostAsync<WarehouseResponse>("/api/warehouses", session.AccessToken, new CreateWarehouseRequest($"Warehouse {suffix}"));
        var product = await PostAsync<ProductResponse>("/api/products", session.AccessToken, new CreateProductRequest($"Product {suffix}", $"SKU-{suffix}"));
        if (receivedQuantity.HasValue)
            await PostAsync<PurchaseReceiptResponse>("/api/purchases/receipts", session.AccessToken,
                new RecordPurchaseReceiptRequest(supplier.Id, warehouse.Id, product.Id, receivedQuantity.Value, $"receipt-{suffix}"));
        return new FulfillmentSources(warehouse, product);
    }

    private static RecordSalesFulfillmentRequest CreateFulfillmentRequest(FulfillmentSources sources, string idempotencyKey,
        decimal quantity = 1m) => new(sources.Warehouse.Id, sources.Product.Id, quantity, idempotencyKey);

    private async Task<Snapshot> SnapshotAsync(Guid workspaceId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new Snapshot(await db.SalesFulfillments.CountAsync(item => item.WorkspaceId == workspaceId),
            await db.InventoryMovements.CountAsync(item => item.WorkspaceId == workspaceId),
            await db.InventoryBalances.Where(item => item.WorkspaceId == workspaceId).Select(item => item.Quantity).SumAsync());
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

    private sealed record FulfillmentSources(WarehouseResponse Warehouse, ProductResponse Product);
    private sealed record Snapshot(int Fulfillments, int Movements, decimal Quantity);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token, object? content = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (content is not null) request.Content = JsonContent.Create(content);
        return await _client.SendAsync(request);
    }
}
