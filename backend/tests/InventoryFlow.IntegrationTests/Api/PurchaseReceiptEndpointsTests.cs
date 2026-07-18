using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InventoryFlow.Application.Features.Authentication;
using InventoryFlow.Application.Features.Products;
using InventoryFlow.Application.Features.Purchases;
using InventoryFlow.Application.Features.Suppliers;
using InventoryFlow.Application.Features.Warehouses;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryFlow.IntegrationTests.Api;

public sealed class PurchaseReceiptEndpointsTests : IClassFixture<AuthenticatedApiFixture>
{
    private readonly AuthenticatedApiFixture _fixture;
    private readonly HttpClient _client;

    public PurchaseReceiptEndpointsTests(AuthenticatedApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    [Fact]
    public async Task Receipts_WithoutAccessToken_ReturnUnauthorized()
    {
        using var post = await _client.PostAsJsonAsync("/api/purchases/receipts",
            new RecordPurchaseReceiptRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1m, "unauthenticated"));
        using var get = await _client.GetAsync("/api/purchases/receipts");

        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
    }

    [Fact]
    public async Task Receipts_CrossWorkspaceSourcesReturnNotFoundAndHistoryIsIsolated()
    {
        var firstSession = await RegisterAsync();
        var secondSession = await RegisterAsync();
        var firstSources = await CreateReceiptSourcesAsync(firstSession);
        var secondSources = await CreateReceiptSourcesAsync(secondSession);
        await PostAsync<PurchaseReceiptResponse>("/api/purchases/receipts", firstSession.AccessToken,
            new RecordPurchaseReceiptRequest(firstSources.Supplier.Id, firstSources.Warehouse.Id, firstSources.Product.Id, 1m,
                $"first-{Guid.NewGuid():N}"));

        using var foreignSupplier = await SendAsync(HttpMethod.Post, "/api/purchases/receipts", secondSession.AccessToken,
            new RecordPurchaseReceiptRequest(firstSources.Supplier.Id, secondSources.Warehouse.Id, secondSources.Product.Id, 1m,
                $"foreign-supplier-{Guid.NewGuid():N}"));
        using var foreignWarehouse = await SendAsync(HttpMethod.Post, "/api/purchases/receipts", secondSession.AccessToken,
            new RecordPurchaseReceiptRequest(secondSources.Supplier.Id, firstSources.Warehouse.Id, secondSources.Product.Id, 1m,
                $"foreign-warehouse-{Guid.NewGuid():N}"));
        using var foreignProduct = await SendAsync(HttpMethod.Post, "/api/purchases/receipts", secondSession.AccessToken,
            new RecordPurchaseReceiptRequest(secondSources.Supplier.Id, secondSources.Warehouse.Id, firstSources.Product.Id, 1m,
                $"foreign-product-{Guid.NewGuid():N}"));
        using var firstHistoryResponse = await SendAsync(HttpMethod.Get, "/api/purchases/receipts", firstSession.AccessToken);
        using var secondHistoryResponse = await SendAsync(HttpMethod.Get, "/api/purchases/receipts", secondSession.AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, foreignSupplier.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignWarehouse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignProduct.StatusCode);
        Assert.Equal(HttpStatusCode.OK, firstHistoryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondHistoryResponse.StatusCode);
        Assert.Single((await firstHistoryResponse.Content.ReadFromJsonAsync<List<PurchaseReceiptResponse>>())!);
        Assert.Empty((await secondHistoryResponse.Content.ReadFromJsonAsync<List<PurchaseReceiptResponse>>())!);
    }

    [Fact]
    public async Task Receipts_WithArchivedSourcesReturnNotFoundWithoutSideEffects()
    {
        var session = await RegisterAsync();

        var archivedSupplierSources = await CreateReceiptSourcesAsync(session);
        using var archivedSupplier = await SendAsync(HttpMethod.Delete, $"/api/suppliers/{archivedSupplierSources.Supplier.Id}", session.AccessToken);
        using var supplierReceipt = await SendAsync(HttpMethod.Post, "/api/purchases/receipts", session.AccessToken,
            CreateReceiptRequest(archivedSupplierSources, $"archived-supplier-{Guid.NewGuid():N}"));

        var archivedProductSources = await CreateReceiptSourcesAsync(session);
        using var archivedProduct = await SendAsync(HttpMethod.Delete, $"/api/products/{archivedProductSources.Product.Id}", session.AccessToken);
        using var productReceipt = await SendAsync(HttpMethod.Post, "/api/purchases/receipts", session.AccessToken,
            CreateReceiptRequest(archivedProductSources, $"archived-product-{Guid.NewGuid():N}"));

        var archivedWarehouseSources = await CreateReceiptSourcesAsync(session);
        using var archivedWarehouse = await SendAsync(HttpMethod.Delete, $"/api/warehouses/{archivedWarehouseSources.Warehouse.Id}", session.AccessToken);
        using var warehouseReceipt = await SendAsync(HttpMethod.Post, "/api/purchases/receipts", session.AccessToken,
            CreateReceiptRequest(archivedWarehouseSources, $"archived-warehouse-{Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.NoContent, archivedSupplier.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, archivedProduct.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, archivedWarehouse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, supplierReceipt.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, productReceipt.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, warehouseReceipt.StatusCode);
        await AssertNoReceiptSideEffectsAsync(session.User.Workspace.Id);
    }

    [Fact]
    public async Task Receipts_WithInvalidRequestReturnBadRequestWithoutSideEffects()
    {
        var session = await RegisterAsync();
        var sources = await CreateReceiptSourcesAsync(session);

        using var response = await SendAsync(HttpMethod.Post, "/api/purchases/receipts", session.AccessToken,
            new RecordPurchaseReceiptRequest(sources.Supplier.Id, sources.Warehouse.Id, sources.Product.Id, 0m,
                $"invalid-{Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoReceiptSideEffectsAsync(session.User.Workspace.Id);
    }

    [Fact]
    public async Task ReceiptHistory_IsOrderedNewestFirst()
    {
        var session = await RegisterAsync();
        var sources = await CreateReceiptSourcesAsync(session);
        var first = await PostAsync<PurchaseReceiptResponse>("/api/purchases/receipts", session.AccessToken,
            CreateReceiptRequest(sources, $"first-{Guid.NewGuid():N}"));
        await Task.Delay(TimeSpan.FromMilliseconds(10));
        var second = await PostAsync<PurchaseReceiptResponse>("/api/purchases/receipts", session.AccessToken,
            CreateReceiptRequest(sources, $"second-{Guid.NewGuid():N}"));
        using var historyResponse = await SendAsync(HttpMethod.Get, "/api/purchases/receipts", session.AccessToken);

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = (await historyResponse.Content.ReadFromJsonAsync<List<PurchaseReceiptResponse>>())!;
        Assert.Collection(history,
            receipt => Assert.Equal(second.Id, receipt.Id),
            receipt => Assert.Equal(first.Id, receipt.Id));
        Assert.True(history[0].ReceivedAtUtc > history[1].ReceivedAtUtc);
    }

    [Fact]
    public async Task Receipt_IsAtomicAndReplayReturnsTheOriginalDocument()
    {
        var session = await RegisterAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var supplier = await PostAsync<SupplierResponse>("/api/suppliers", session.AccessToken, new CreateSupplierRequest($"Supplier {suffix}"));
        var warehouse = await PostAsync<WarehouseResponse>("/api/warehouses", session.AccessToken, new CreateWarehouseRequest($"Warehouse {suffix}"));
        var product = await PostAsync<ProductResponse>("/api/products", session.AccessToken, new CreateProductRequest($"Product {suffix}", $"SKU-{suffix}"));
        var request = new RecordPurchaseReceiptRequest(supplier.Id, warehouse.Id, product.Id, 2.5m, $"receipt-{suffix}");

        using var first = await SendAsync(HttpMethod.Post, "/api/purchases/receipts", session.AccessToken, request);
        using var replay = await SendAsync(HttpMethod.Post, "/api/purchases/receipts", session.AccessToken, request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        var posted = (await first.Content.ReadFromJsonAsync<PurchaseReceiptResponse>())!;
        var replayed = (await replay.Content.ReadFromJsonAsync<PurchaseReceiptResponse>())!;
        Assert.Equal(posted.Id, replayed.Id);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var receipt = await db.PurchaseReceipts.SingleAsync(item => item.Id == posted.Id);
        var movement = await db.InventoryMovements.SingleAsync(item => item.Id == receipt.InventoryMovementId);
        var balance = await db.InventoryBalances.SingleAsync(item => item.WorkspaceId == session.User.Workspace.Id);
        Assert.Equal(InventoryMovementType.Receipt, movement.Type);
        Assert.Equal(2.5m, balance.Quantity);
        Assert.Equal(1, await db.PurchaseReceipts.CountAsync(item => item.WorkspaceId == session.User.Workspace.Id));
        Assert.Equal(1, await db.InventoryMovements.CountAsync(item => item.WorkspaceId == session.User.Workspace.Id));
    }

    [Fact]
    public async Task Receipt_ConcurrentReplayCreatesOneDocumentAndMovement()
    {
        var session = await RegisterAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var supplier = await PostAsync<SupplierResponse>("/api/suppliers", session.AccessToken, new CreateSupplierRequest($"Supplier {suffix}"));
        var warehouse = await PostAsync<WarehouseResponse>("/api/warehouses", session.AccessToken, new CreateWarehouseRequest($"Warehouse {suffix}"));
        var product = await PostAsync<ProductResponse>("/api/products", session.AccessToken, new CreateProductRequest($"Product {suffix}", $"SKU-{suffix}"));
        var request = new RecordPurchaseReceiptRequest(supplier.Id, warehouse.Id, product.Id, 1m, $"receipt-{suffix}");

        var responses = await Task.WhenAll(
            SendAsync(HttpMethod.Post, "/api/purchases/receipts", session.AccessToken, request),
            SendAsync(HttpMethod.Post, "/api/purchases/receipts", session.AccessToken, request));
        using var first = responses[0];
        using var second = responses[1];
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.PurchaseReceipts.CountAsync(item => item.WorkspaceId == session.User.Workspace.Id));
        Assert.Equal(1, await db.InventoryMovements.CountAsync(item => item.WorkspaceId == session.User.Workspace.Id));
        Assert.Equal(1m, await db.InventoryBalances.Where(item => item.WorkspaceId == session.User.Workspace.Id).Select(item => item.Quantity).SingleAsync());
    }

    private async Task<ReceiptSources> CreateReceiptSourcesAsync(AuthenticationResponse session)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var supplier = await PostAsync<SupplierResponse>("/api/suppliers", session.AccessToken, new CreateSupplierRequest($"Supplier {suffix}"));
        var warehouse = await PostAsync<WarehouseResponse>("/api/warehouses", session.AccessToken, new CreateWarehouseRequest($"Warehouse {suffix}"));
        var product = await PostAsync<ProductResponse>("/api/products", session.AccessToken, new CreateProductRequest($"Product {suffix}", $"SKU-{suffix}"));
        return new ReceiptSources(supplier, warehouse, product);
    }

    private static RecordPurchaseReceiptRequest CreateReceiptRequest(ReceiptSources sources, string idempotencyKey) =>
        new(sources.Supplier.Id, sources.Warehouse.Id, sources.Product.Id, 1m, idempotencyKey);

    private async Task AssertNoReceiptSideEffectsAsync(Guid workspaceId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await db.PurchaseReceipts.CountAsync(item => item.WorkspaceId == workspaceId));
        Assert.Equal(0, await db.InventoryMovements.CountAsync(item => item.WorkspaceId == workspaceId));
        Assert.Equal(0, await db.InventoryBalances.CountAsync(item => item.WorkspaceId == workspaceId));
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
        using var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserCommand($"User {suffix}", $"user-{suffix}@example.test", "Password!12345"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
    }

    private sealed record ReceiptSources(SupplierResponse Supplier, WarehouseResponse Warehouse, ProductResponse Product);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token, object? content = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (content is not null) request.Content = JsonContent.Create(content);
        return await _client.SendAsync(request);
    }
}
