using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using InventoryFlow.Application.Features.Authentication;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Identity;
using InventoryFlow.Infrastructure.Persistence;
using InventoryFlow.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace InventoryFlow.IntegrationTests.Api;

/// <summary>
/// Verifies workspace migration backfill and current-workspace tenancy isolation against SQL Server.
/// </summary>
public sealed class WorkspaceMigrationAndTenancyTests : IClassFixture<WorkspaceMigrationFixture>
{
    private const string RefreshCookieName = "inventory_flow_refresh";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly WorkspaceMigrationFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceMigrationAndTenancyTests"/> class.
    /// </summary>
    /// <param name="fixture">The SQL Server-backed workspace test fixture.</param>
    public WorkspaceMigrationAndTenancyTests(WorkspaceMigrationFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Backfills one owner workspace for a legacy user and returns it from login and refresh sessions.
    /// </summary>
    [Fact]
    public async Task AddWorkspaces_BackfillsLegacyUser_AndReturnsWorkspaceFromLoginAndRefresh()
    {
        // Arrange
        using var client = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
        });

        // Act
        using var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginUserCommand(_fixture.LegacyUser.Email!, WorkspaceMigrationFixture.Password));
        var loginSession = await ReadSessionAsync(login);
        var refreshToken = GetRefreshCookieValue(login);
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", $"{RefreshCookieName}={refreshToken}");
        using var refresh = await client.SendAsync(refreshRequest);
        var refreshSession = await ReadSessionAsync(refresh);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var memberships = await dbContext.WorkspaceMembers
            .Where(member => member.UserId == _fixture.LegacyUser.Id)
            .ToListAsync();

        // Assert
        var membership = Assert.Single(memberships);
        Assert.Equal(WorkspaceMemberRole.Owner, membership.Role);
        var workspace = await dbContext.Workspaces.SingleAsync(item => item.Id == membership.WorkspaceId);
        Assert.Equal(workspace.Id, loginSession.User.Workspace.Id);
        Assert.Equal(workspace.Name, loginSession.User.Workspace.Name);
        Assert.Equal(loginSession.User.Workspace, refreshSession.User.Workspace);
    }

    /// <summary>
    /// Resolves only the authenticated user's workspace and fails closed when owner membership is ambiguous.
    /// </summary>
    [Fact]
    public async Task CurrentWorkspaceResolver_EnforcesAuthenticatedUserBoundary_AndFailsClosedWhenAmbiguous()
    {
        // Arrange
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var firstUser = CreateUser("first-owner@example.test", "First owner");
        var secondUser = CreateUser("second-owner@example.test", "Second owner");
        var firstWorkspace = new Workspace(Guid.NewGuid(), "First workspace", DateTimeOffset.UtcNow);
        var secondWorkspace = new Workspace(Guid.NewGuid(), "Second workspace", DateTimeOffset.UtcNow);
        dbContext.AddRange(firstUser, secondUser, firstWorkspace, secondWorkspace);
        dbContext.AddRange(
            new WorkspaceMember(Guid.NewGuid(), firstWorkspace.Id, firstUser.Id, WorkspaceMemberRole.Owner, DateTimeOffset.UtcNow),
            new WorkspaceMember(Guid.NewGuid(), secondWorkspace.Id, secondUser.Id, WorkspaceMemberRole.Owner, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();

        var resolver = CreateResolver(dbContext, firstUser.Id);

        // Act
        var currentWorkspace = await resolver.GetAsync();

        // Assert
        Assert.NotNull(currentWorkspace);
        Assert.Equal(firstWorkspace.Id, currentWorkspace.Id);
        Assert.NotEqual(secondWorkspace.Id, currentWorkspace.Id);

        // Arrange
        var additionalWorkspace = new Workspace(Guid.NewGuid(), "Additional workspace", DateTimeOffset.UtcNow);
        dbContext.Workspaces.Add(additionalWorkspace);
        dbContext.WorkspaceMembers.Add(new WorkspaceMember(
            Guid.NewGuid(),
            additionalWorkspace.Id,
            firstUser.Id,
            WorkspaceMemberRole.Owner,
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();

        // Act
        var ambiguousWorkspace = await resolver.GetAsync();

        // Assert
        Assert.Null(ambiguousWorkspace);
    }

    private static CurrentWorkspaceResolver CreateResolver(ApplicationDbContext dbContext, Guid userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            "Test");
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            },
        };

        return new CurrentWorkspaceResolver(accessor, dbContext);
    }

    private static ApplicationUser CreateUser(string email, string displayName) => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        UserName = email,
        NormalizedEmail = email.ToUpperInvariant(),
        NormalizedUserName = email.ToUpperInvariant(),
        DisplayName = displayName,
        SecurityStamp = Guid.NewGuid().ToString(),
        ConcurrencyStamp = Guid.NewGuid().ToString(),
    };

    private static async Task<AuthenticationResponse> ReadSessionAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<AuthenticationResponse>(JsonOptions);
        return Assert.IsType<AuthenticationResponse>(session);
    }

    private static string GetRefreshCookieValue(HttpResponseMessage response)
    {
        var header = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        var cookie = header.Split(';', 2)[0].Split('=', 2);
        Assert.Equal(RefreshCookieName, cookie[0]);
        return Uri.UnescapeDataString(cookie[1]);
    }
}

/// <summary>
/// Hosts a SQL Server database first migrated to the pre-workspace schema, then to the current schema.
/// </summary>
public sealed class WorkspaceMigrationFixture : IAsyncLifetime
{
    /// <summary>
    /// Password assigned to the seeded pre-workspace Identity user.
    /// </summary>
    public const string Password = "Password!12345";

    private const string ConnectionStringEnvironmentVariable = "ConnectionStrings__InventoryFlowDatabase";
    private const string SigningKeyEnvironmentVariable = "Jwt__SigningKey";
    private const string IssuerEnvironmentVariable = "Jwt__Issuer";
    private const string AudienceEnvironmentVariable = "Jwt__Audience";
    private const string PreviousMigration = "20260718100649_AddRefreshTokenFamilies";

    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();
    private readonly string? _originalConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
    private readonly string? _originalSigningKey = Environment.GetEnvironmentVariable(SigningKeyEnvironmentVariable);
    private readonly string? _originalIssuer = Environment.GetEnvironmentVariable(IssuerEnvironmentVariable);
    private readonly string? _originalAudience = Environment.GetEnvironmentVariable(AudienceEnvironmentVariable);

    /// <summary>
    /// Gets the API host connected to the migrated database.
    /// </summary>
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    /// <summary>
    /// Gets the legacy user seeded before the workspace migration runs.
    /// </summary>
    public ApplicationUser LegacyUser { get; private set; } = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _sqlServer.StartAsync();
        var databaseName = $"InventoryFlowWorkspaceTests_{Guid.NewGuid():N}";
        await using (var connection = new SqlConnection(_sqlServer.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync();
        }

        var connectionString = new SqlConnectionStringBuilder(_sqlServer.GetConnectionString())
        {
            InitialCatalog = databaseName,
        }.ConnectionString;
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, connectionString);
        Environment.SetEnvironmentVariable(SigningKeyEnvironmentVariable, "test-signing-key-that-is-at-least-thirty-two-bytes-long");
        Environment.SetEnvironmentVariable(IssuerEnvironmentVariable, "InventoryFlow.Test");
        Environment.SetEnvironmentVariable(AudienceEnvironmentVariable, "InventoryFlow.Test.Web");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using (var dbContext = new ApplicationDbContext(options))
        {
            await dbContext.Database.MigrateAsync(PreviousMigration);
            LegacyUser = CreateLegacyUser();
            dbContext.Users.Add(LegacyUser);
            await dbContext.SaveChangesAsync();
            await dbContext.Database.MigrateAsync();
        }

        Factory = new WorkspaceApiFactory();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, _originalConnectionString);
        Environment.SetEnvironmentVariable(SigningKeyEnvironmentVariable, _originalSigningKey);
        Environment.SetEnvironmentVariable(IssuerEnvironmentVariable, _originalIssuer);
        Environment.SetEnvironmentVariable(AudienceEnvironmentVariable, _originalAudience);
        await _sqlServer.DisposeAsync();
    }

    private static ApplicationUser CreateLegacyUser()
    {
        const string email = "legacy-owner@example.test";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            DisplayName = "Legacy owner",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        };
        user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, Password);
        return user;
    }

    private sealed class WorkspaceApiFactory : WebApplicationFactory<Program>
    {
        /// <inheritdoc />
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }
    }
}
