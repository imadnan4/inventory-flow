using InventoryFlow.Infrastructure.Identity;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryFlow.Infrastructure.DependencyInjection;

/// <summary>
/// Registers infrastructure-layer services.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds SQL Server persistence and ASP.NET Core Identity services.
    /// </summary>
    /// <param name="services">The dependency-injection service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The supplied <paramref name="services"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the database connection string is missing.</exception>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("InventoryFlowDatabase");
        ArgumentException.ThrowIfNullOrWhiteSpace(
            connectionString,
            "ConnectionStrings:InventoryFlowDatabase");

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(
            connectionString,
            sqlServerOptions => sqlServerOptions.EnableRetryOnFailure()));
        services.AddDataProtection();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        return services;
    }
}
