using Microsoft.AspNetCore.Mvc.Testing;

namespace InventoryFlow.IntegrationTests.Api;

/// <summary>
/// Hosts the API with test-only process configuration.
/// </summary>
public sealed class InventoryFlowApiFactory : WebApplicationFactory<Program>
{
    private const string ConnectionStringEnvironmentVariable =
        "ConnectionStrings__InventoryFlowDatabase";

    private readonly string? _originalConnectionString = Environment.GetEnvironmentVariable(
        ConnectionStringEnvironmentVariable);

    /// <summary>
    /// Initializes a new instance of the <see cref="InventoryFlowApiFactory"/> class.
    /// </summary>
    public InventoryFlowApiFactory()
    {
        Environment.SetEnvironmentVariable(
            ConnectionStringEnvironmentVariable,
            "Server=inventory-flow-test;Database=InventoryFlowTests;Integrated Security=True;TrustServerCertificate=True");
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Environment.SetEnvironmentVariable(
                ConnectionStringEnvironmentVariable,
                _originalConnectionString);
        }

        base.Dispose(disposing);
    }
}
