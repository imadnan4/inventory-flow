using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InventoryFlow.Application.Features.Authentication;
using InventoryFlow.Application.Features.Inventory;
using InventoryFlow.Application.Features.Products;
using InventoryFlow.Application.Features.Warehouses;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryFlow.IntegrationTests.Api;

/// <summary>Verifies inventory receipt, issue, balance, and idempotency behavior against SQL Server.</summary>
public sealed class InventoryEndpointsTests : IClassFixture<AuthenticatedApiFixture>
{
    private readonly HttpClient _client;
    private readonly AuthenticatedApiFixture _fixture;

    /// <summary>Initializes the test client.</summary>
    public InventoryEndpointsTests(AuthenticatedApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    /// <summary>Records a decimal receipt once, prevents an overdraft, and exposes the resulting balance.</summary>
    [Fact]
    public async Task Inventory_ReceiptIssueAndBalance_AreIdempotentAndNonnegative()
    {
        var session = await RegisterSessionAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var warehouse = await PostAsync<WarehouseResponse>("/api/warehouses", session.AccessToken, new CreateWarehouseRequest($"Warehouse {suffix}"));
        var product = await PostAsync<ProductResponse>("/api/products", session.AccessToken, new CreateProductRequest($"Product {suffix}", $"SKU-{suffix}"));
        var receiptRequest = new RecordInventoryMovementRequest(warehouse.Id, product.Id, 2.5000m, $"receipt-{suffix}");

        using var receipt = await SendAsync(HttpMethod.Post, "/api/inventory/receipts", session.AccessToken, receiptRequest);
        using var duplicateReceipt = await SendAsync(HttpMethod.Post, "/api/inventory/receipts", session.AccessToken, receiptRequest);
        using var issue = await SendAsync(HttpMethod.Post, "/api/inventory/issues", session.AccessToken,
            new RecordInventoryMovementRequest(warehouse.Id, product.Id, 1.2500m, $"issue-{suffix}"));
        using var overdraft = await SendAsync(HttpMethod.Post, "/api/inventory/issues", session.AccessToken,
            new RecordInventoryMovementRequest(warehouse.Id, product.Id, 1.2501m, $"overdraft-{suffix}"));
        using var balances = await SendAsync(HttpMethod.Get, $"/api/inventory/balances?warehouseId={warehouse.Id}", session.AccessToken);

        Assert.Equal(HttpStatusCode.Created, receipt.StatusCode);
        Assert.Equal(HttpStatusCode.Created, duplicateReceipt.StatusCode);
        Assert.Equal(HttpStatusCode.Created, issue.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, overdraft.StatusCode);
        var balance = Assert.Single(await balances.Content.ReadFromJsonAsync<List<InventoryBalanceResponse>>() ?? []);
        Assert.Equal(1.2500m, balance.Quantity);
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await dbContext.InventoryMovements.CountAsync(movement => movement.WorkspaceId == session.User.Workspace.Id));
    }

    /// <summary>Persists the largest decimal(18,4) quantity accepted by the inventory ledger.</summary>
    [Fact]
    public async Task Receipt_WithMaximumDecimalQuantity_IsPersisted()
    {
        var session = await RegisterSessionAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var warehouse = await PostAsync<WarehouseResponse>("/api/warehouses", session.AccessToken, new CreateWarehouseRequest($"Warehouse {suffix}"));
        var product = await PostAsync<ProductResponse>("/api/products", session.AccessToken, new CreateProductRequest($"Product {suffix}", $"SKU-{suffix}"));

        using var response = await SendAsync(HttpMethod.Post, "/api/inventory/receipts", session.AccessToken,
            new RecordInventoryMovementRequest(warehouse.Id, product.Id, InventoryMovement.MaxQuantity, $"maximum-{suffix}"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var balance = await dbContext.InventoryBalances.SingleAsync(item => item.WorkspaceId == session.User.Workspace.Id);
        var movement = await dbContext.InventoryMovements.SingleAsync(item => item.WorkspaceId == session.User.Workspace.Id);
        Assert.Equal(InventoryMovement.MaxQuantity, balance.Quantity);
        Assert.Equal(InventoryMovement.MaxQuantity, movement.Quantity);
        Assert.Equal(InventoryMovement.MaxQuantity, movement.BalanceAfterQuantity);
    }

    /// <summary>Rejects a receipt quantity one unit above the decimal(18,4) maximum before persistence.</summary>
    [Fact]
    public async Task Receipt_ExceedingMaximumDecimalQuantity_IsRejectedWithoutPersistence()
    {
        var session = await RegisterSessionAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var warehouse = await PostAsync<WarehouseResponse>("/api/warehouses", session.AccessToken, new CreateWarehouseRequest($"Warehouse {suffix}"));
        var product = await PostAsync<ProductResponse>("/api/products", session.AccessToken, new CreateProductRequest($"Product {suffix}", $"SKU-{suffix}"));

        using var response = await SendAsync(HttpMethod.Post, "/api/inventory/receipts", session.AccessToken,
            new RecordInventoryMovementRequest(warehouse.Id, product.Id, 100000000000000m, $"too-large-{suffix}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await dbContext.InventoryMovements.CountAsync(item => item.WorkspaceId == session.User.Workspace.Id));
        Assert.Equal(0, await dbContext.InventoryBalances.CountAsync(item => item.WorkspaceId == session.User.Workspace.Id));
    }

    /// <summary>Rejects a receipt that would overflow the persisted decimal(18,4) balance.</summary>
    [Fact]
    public async Task Receipt_OverflowingExistingBalance_IsRejectedWithoutChangingPersistedBalance()
    {
        var session = await RegisterSessionAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var warehouse = await PostAsync<WarehouseResponse>("/api/warehouses", session.AccessToken, new CreateWarehouseRequest($"Warehouse {suffix}"));
        var product = await PostAsync<ProductResponse>("/api/products", session.AccessToken, new CreateProductRequest($"Product {suffix}", $"SKU-{suffix}"));
        var initialQuantity = InventoryMovement.MaxQuantity - 0.0001m;
        await PostAsync<InventoryMovementResponse>("/api/inventory/receipts", session.AccessToken,
            new RecordInventoryMovementRequest(warehouse.Id, product.Id, initialQuantity, $"initial-{suffix}"));

        using var response = await SendAsync(HttpMethod.Post, "/api/inventory/receipts", session.AccessToken,
            new RecordInventoryMovementRequest(warehouse.Id, product.Id, 0.0002m, $"overflow-{suffix}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var balance = await dbContext.InventoryBalances.SingleAsync(item => item.WorkspaceId == session.User.Workspace.Id);
        Assert.Equal(initialQuantity, balance.Quantity);
        Assert.Equal(1, await dbContext.InventoryMovements.CountAsync(item => item.WorkspaceId == session.User.Workspace.Id));
    }

    /// <summary>Prevents archiving either inventory source while scoped stock remains on hand.</summary>
    [Fact]
    public async Task Archive_WithOnHandBalance_ReturnsConflictForProductAndWarehouse()
    {
        var session = await RegisterSessionAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var warehouse = await PostAsync<WarehouseResponse>("/api/warehouses", session.AccessToken,
            new CreateWarehouseRequest($"Warehouse {suffix}"));
        var product = await PostAsync<ProductResponse>("/api/products", session.AccessToken,
            new CreateProductRequest($"Product {suffix}", $"SKU-{suffix}"));
        await PostAsync<InventoryMovementResponse>("/api/inventory/receipts", session.AccessToken,
            new RecordInventoryMovementRequest(warehouse.Id, product.Id, 1m, $"receipt-{suffix}"));

        using var productArchive = await SendAsync(HttpMethod.Delete, $"/api/products/{product.Id}", session.AccessToken);
        using var warehouseArchive = await SendAsync(HttpMethod.Delete, $"/api/warehouses/{warehouse.Id}", session.AccessToken);

        Assert.Equal(HttpStatusCode.Conflict, productArchive.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, warehouseArchive.StatusCode);
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Null((await dbContext.Products.SingleAsync(item => item.Id == product.Id)).ArchivedAtUtc);
        Assert.Null((await dbContext.Warehouses.SingleAsync(item => item.Id == warehouse.Id)).ArchivedAtUtc);
    }

    /// <summary>Serializes source archival against a concurrent receipt so stock cannot enter an archived product.</summary>
    [Fact]
    public async Task ProductArchive_AndConcurrentReceipt_CannotCreateMovementForAnArchivedProduct()
    {
        var session = await RegisterSessionAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var warehouse = await PostAsync<WarehouseResponse>("/api/warehouses", session.AccessToken,
            new CreateWarehouseRequest($"Warehouse {suffix}"));
        var product = await PostAsync<ProductResponse>("/api/products", session.AccessToken,
            new CreateProductRequest($"Product {suffix}", $"SKU-{suffix}"));

        var receiptTask = SendAsync(HttpMethod.Post, "/api/inventory/receipts", session.AccessToken,
            new RecordInventoryMovementRequest(warehouse.Id, product.Id, 1m, $"receipt-{suffix}"));
        var archiveTask = SendAsync(HttpMethod.Delete, $"/api/products/{product.Id}", session.AccessToken);
        var responses = await Task.WhenAll(receiptTask, archiveTask);
        using var receipt = responses[0];
        using var archive = responses[1];

        Assert.True(receipt.StatusCode is HttpStatusCode.Created or HttpStatusCode.NotFound);
        Assert.True(archive.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Conflict);
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedProduct = await dbContext.Products.SingleAsync(item => item.Id == product.Id);
        var movementCount = await dbContext.InventoryMovements.CountAsync(item => item.ProductId == product.Id);
        Assert.False(persistedProduct.ArchivedAtUtc.HasValue && movementCount > 0);
    }

    /// <summary>Serializes source archival against a concurrent receipt so stock cannot enter an archived warehouse.</summary>
    [Fact]
    public async Task WarehouseArchive_AndConcurrentReceipt_CannotCreateMovementForAnArchivedWarehouse()
    {
        var session = await RegisterSessionAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var warehouse = await PostAsync<WarehouseResponse>("/api/warehouses", session.AccessToken,
            new CreateWarehouseRequest($"Warehouse {suffix}"));
        var product = await PostAsync<ProductResponse>("/api/products", session.AccessToken,
            new CreateProductRequest($"Product {suffix}", $"SKU-{suffix}"));

        var receiptTask = SendAsync(HttpMethod.Post, "/api/inventory/receipts", session.AccessToken,
            new RecordInventoryMovementRequest(warehouse.Id, product.Id, 1m, $"receipt-{suffix}"));
        var archiveTask = SendAsync(HttpMethod.Delete, $"/api/warehouses/{warehouse.Id}", session.AccessToken);
        var responses = await Task.WhenAll(receiptTask, archiveTask);
        using var receipt = responses[0];
        using var archive = responses[1];

        Assert.True(receipt.StatusCode is HttpStatusCode.Created or HttpStatusCode.NotFound);
        Assert.True(archive.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Conflict);
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedWarehouse = await dbContext.Warehouses.SingleAsync(item => item.Id == warehouse.Id);
        var movementCount = await dbContext.InventoryMovements.CountAsync(item => item.WarehouseId == warehouse.Id);
        Assert.False(persistedWarehouse.ArchivedAtUtc.HasValue && movementCount > 0);
    }

    private async Task<T> PostAsync<T>(string path, string token, object content)
    {
        using var response = await SendAsync(HttpMethod.Post, path, token, content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<T>();
        return Assert.IsType<T>(payload);
    }

    private async Task<AuthenticationResponse> RegisterSessionAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserCommand($"User {suffix}", $"user-{suffix}@example.test", "Password!12345"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();
        return Assert.IsType<AuthenticationResponse>(payload);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token, object? content = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (content is not null) request.Content = JsonContent.Create(content);
        return await _client.SendAsync(request);
    }
}
