using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InventoryFlow.IntegrationTests.Api;

/// <summary>
/// Verifies the API's operational health endpoint.
/// </summary>
public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthEndpointTests"/> class.
    /// </summary>
    /// <param name="factory">The in-memory API host.</param>
    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Returns an OK response when the API host is available.
    /// </summary>
    [Fact]
    public async Task GetHealth_WhenApiIsRunning_ReturnsOk()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
