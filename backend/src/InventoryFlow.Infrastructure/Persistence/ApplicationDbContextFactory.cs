using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InventoryFlow.Infrastructure.Persistence;

/// <summary>
/// Creates <see cref="ApplicationDbContext"/> instances for EF Core design-time operations.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    /// <summary>
    /// Creates a context using the database connection string supplied through the environment.
    /// </summary>
    /// <param name="args">Unused command-line arguments supplied by the EF Core tools.</param>
    /// <returns>A configured database context.</returns>
    /// <exception cref="ArgumentException">Thrown when the database connection string is missing.</exception>
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__InventoryFlowDatabase");
        ArgumentException.ThrowIfNullOrWhiteSpace(
            connectionString,
            "ConnectionStrings__InventoryFlowDatabase");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}
