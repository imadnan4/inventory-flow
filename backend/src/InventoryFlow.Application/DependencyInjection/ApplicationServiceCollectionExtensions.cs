using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryFlow.Application.DependencyInjection;

/// <summary>
/// Registers application-layer services.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Adds MediatR handlers and FluentValidation validators from the application assembly.
    /// </summary>
    /// <param name="services">The dependency-injection service collection.</param>
    /// <returns>The supplied <paramref name="services"/> instance.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = typeof(ApplicationServiceCollectionExtensions).Assembly;
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
