using FluentAssertions;
using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Tests.Features.Suppliers;

public class SupplierTests
{
    [Fact]
    public void Supplier_Create_With_Valid_Data_Has_Correct_Properties()
    {
        var supplier = new Supplier(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Acme Corp",
            DateTimeOffset.UtcNow);

        supplier.Name.Should().Be("Acme Corp");
        supplier.WorkspaceId.Should().NotBeEmpty();
        supplier.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Supplier_NormalizeName_Trimms_Whitespace()
    {
        var name = Supplier.NormalizeName("  Acme Corp  ");

        name.Should().Be("Acme Corp");
    }
}