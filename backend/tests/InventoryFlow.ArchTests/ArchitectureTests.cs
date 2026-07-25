using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace InventoryFlow.ArchTests;

public class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(InventoryFlow.Domain.Entities.Product).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(InventoryFlow.Application.Features.Products.CreateProductHandler).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(InventoryFlow.Infrastructure.Persistence.ApplicationDbContext).Assembly;
    private static readonly Assembly ApiAssembly = typeof(InventoryFlow.Api.Controllers.AuthController).Assembly;

    [Fact]
    public void Domain_Should_Not_Depend_On_Infrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureAssembly.FullName!)
            .GetResult();

        Assert.True(result);
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_Api()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApiAssembly.FullName!)
            .GetResult();

        Assert.True(result);
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_Application()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApplicationAssembly.FullName!)
            .GetResult();

        Assert.True(result);
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureAssembly.FullName!)
            .GetResult();

        Assert.True(result);
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApiAssembly.FullName!)
            .GetResult();

        Assert.True(result);
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApiAssembly.FullName!)
            .GetResult();

        Assert.True(result);
    }

    [Fact]
    public void Domain_Should_Not_Have_Framework_Dependencies()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result);
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_AspNetCore()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result);
    }

    [Fact]
    public void Application_Should_Not_Depend_On_EFCore()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result);
    }
}