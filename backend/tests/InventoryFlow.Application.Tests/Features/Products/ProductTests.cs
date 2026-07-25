using FluentAssertions;
using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Tests.Features.Products;

public class ProductTests
{
    [Fact]
    public void Product_Create_With_Valid_Data_Has_Correct_Properties()
    {
        var product = new Product(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Product",
            "TP-001",
            DateTimeOffset.UtcNow);

        product.Name.Should().Be("Test Product");
        product.Sku.Should().Be("TP-001");
        product.Id.Should().NotBeEmpty();
        product.WorkspaceId.Should().NotBeEmpty();
    }

    [Fact]
    public void Product_NormalizeName_Trimms_Whitespace()
    {
        var name = Product.NormalizeName("  Test Product  ");

        name.Should().Be("Test Product");
    }

    [Fact]
    public void Product_NormalizeSku_Converts_To_Upper()
    {
        var sku = Product.NormalizeSku("tp-001");

        sku.Should().Be("TP-001");
    }
}