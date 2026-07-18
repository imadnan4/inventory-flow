using InventoryFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace InventoryFlow.IntegrationTests.Api;

/// <summary>
/// Provides an API host backed by an isolated, migrated SQL Server database.
/// </summary>
public sealed class AuthenticatedApiFixture : IAsyncLifetime
{
    private const string ConnectionStringEnvironmentVariable =
        "ConnectionStrings__InventoryFlowDatabase";
    private const string SigningKeyEnvironmentVariable = "Jwt__SigningKey";
    private const string IssuerEnvironmentVariable = "Jwt__Issuer";
    private const string AudienceEnvironmentVariable = "Jwt__Audience";

    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();
    private readonly string? _originalConnectionString = Environment.GetEnvironmentVariable(
        ConnectionStringEnvironmentVariable);
    private readonly string? _originalSigningKey = Environment.GetEnvironmentVariable(
        SigningKeyEnvironmentVariable);
    private readonly string? _originalIssuer = Environment.GetEnvironmentVariable(
        IssuerEnvironmentVariable);
    private readonly string? _originalAudience = Environment.GetEnvironmentVariable(
        AudienceEnvironmentVariable);

    /// <summary>
    /// Gets the hosted API factory.
    /// </summary>
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _sqlServer.StartAsync();

        var databaseName = $"InventoryFlowTests_{Guid.NewGuid():N}";
        await using (var connection = new SqlConnection(_sqlServer.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync();
        }

        var connectionStringBuilder = new SqlConnectionStringBuilder(_sqlServer.GetConnectionString())
        {
            InitialCatalog = databaseName,
        };
        Environment.SetEnvironmentVariable(
            ConnectionStringEnvironmentVariable,
            connectionStringBuilder.ConnectionString);
        Environment.SetEnvironmentVariable(
            SigningKeyEnvironmentVariable,
            "test-signing-key-that-is-at-least-thirty-two-bytes-long");
        Environment.SetEnvironmentVariable(IssuerEnvironmentVariable, "InventoryFlow.Test");
        Environment.SetEnvironmentVariable(AudienceEnvironmentVariable, "InventoryFlow.Test.Web");
        Factory = new AuthenticatedApiFactory();

        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        Environment.SetEnvironmentVariable(
            ConnectionStringEnvironmentVariable,
            _originalConnectionString);
        Environment.SetEnvironmentVariable(SigningKeyEnvironmentVariable, _originalSigningKey);
        Environment.SetEnvironmentVariable(IssuerEnvironmentVariable, _originalIssuer);
        Environment.SetEnvironmentVariable(AudienceEnvironmentVariable, _originalAudience);
        await _sqlServer.DisposeAsync();
    }

    private sealed class AuthenticatedApiFactory : WebApplicationFactory<Program>
    {
        /// <inheritdoc />
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }
    }
}
