using System.Text;
using InventoryFlow.Application.Features.Authentication;
using InventoryFlow.Application.Features.Products;
using InventoryFlow.Infrastructure.Authentication;
using InventoryFlow.Infrastructure.Identity;
using InventoryFlow.Application.Features.Warehouses;
using InventoryFlow.Infrastructure.Warehouses;
using InventoryFlow.Application.Features.Inventory;
using InventoryFlow.Infrastructure.Inventory;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using InventoryFlow.Infrastructure.Persistence;
using InventoryFlow.Infrastructure.Products;
using InventoryFlow.Infrastructure.Tenancy;
using InventoryFlow.Application.Common.Tenancy;
using Microsoft.AspNetCore.DataProtection;
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

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => Encoding.UTF8.GetByteCount(options.SigningKey) >= 32, "Jwt signing key must be at least 32 bytes.")
            .ValidateOnStart();
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "display_name"
                };
            });
        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICurrentWorkspace, CurrentWorkspaceResolver>();
        services.AddScoped<JwtAccessTokenIssuer>();
        services.AddSingleton<RefreshTokenGenerator>();
        services.AddScoped<IAuthenticationService, IdentityAuthenticationService>();
        services.AddScoped<IProductCatalog, EfProductCatalog>();
        services.AddScoped<IWarehouseCatalog, EfWarehouseCatalog>();
        services.AddScoped<IInventoryLedger, EfInventoryLedger>();

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(
            connectionString,
            sqlServerOptions => sqlServerOptions.EnableRetryOnFailure()));
        services.AddDataProtection()
            .SetApplicationName("InventoryFlow")
            .PersistKeysToDbContext<ApplicationDbContext>();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        return services;
    }
}
