using Microsoft.AspNetCore.DataProtection.KeyManagement;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly string? _originalSigningKey = Environment.GetEnvironmentVariable("Jwt__SigningKey");
    private readonly string? _originalIssuer = Environment.GetEnvironmentVariable("Jwt__Issuer");
    private readonly string? _originalAudience = Environment.GetEnvironmentVariable("Jwt__Audience");

    /// <summary>
    /// Initializes a new instance of the <see cref="InventoryFlowApiFactory"/> class.
    /// </summary>
    public InventoryFlowApiFactory()
    {
        Environment.SetEnvironmentVariable(
            ConnectionStringEnvironmentVariable,
            "Server=inventory-flow-test;Database=InventoryFlowTests;Integrated Security=True;TrustServerCertificate=True");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "test-signing-key-that-is-at-least-thirty-two-bytes-long");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "InventoryFlow.Test");
        Environment.SetEnvironmentVariable("Jwt__Audience", "InventoryFlow.Test.Web");
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services => services
            .PostConfigure<KeyManagementOptions>(options =>
                options.XmlRepository = new InMemoryXmlRepository()));
    }

    private sealed class InMemoryXmlRepository : IXmlRepository
    {
        private readonly List<XElement> _elements = [];

        public IReadOnlyCollection<XElement> GetAllElements()
        {
            lock (_elements)
            {
                return _elements.Select(element => new XElement(element)).ToArray();
            }
        }

        public void StoreElement(XElement element, string friendlyName)
        {
            ArgumentNullException.ThrowIfNull(element);

            lock (_elements)
            {
                _elements.Add(new XElement(element));
            }
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Environment.SetEnvironmentVariable(
                ConnectionStringEnvironmentVariable,
                _originalConnectionString);
            Environment.SetEnvironmentVariable("Jwt__SigningKey", _originalSigningKey);
            Environment.SetEnvironmentVariable("Jwt__Issuer", _originalIssuer);
            Environment.SetEnvironmentVariable("Jwt__Audience", _originalAudience);
        }

        base.Dispose(disposing);
    }
}
