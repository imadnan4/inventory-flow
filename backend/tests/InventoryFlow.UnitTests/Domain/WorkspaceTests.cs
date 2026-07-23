using InventoryFlow.Domain.Entities;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.UnitTests.Domain;

/// <summary>Verifies workspace invariants.</summary>
public sealed class WorkspaceTests
{
    /// <summary>Trims a valid workspace name.</summary>
    [Fact]
    public void Constructor_WithValidInput_CreatesWorkspace()
    {
        var workspace = new Workspace(Guid.NewGuid(), "  Operations  ", DateTimeOffset.UtcNow);
        Assert.Equal("Operations", workspace.Name);
    }

    /// <summary>Rejects invalid workspace names.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_ThrowsDomainException(string name) =>
        Assert.IsType<DomainException>(Record.Exception(() => new Workspace(Guid.NewGuid(), name, DateTimeOffset.UtcNow)));

    /// <summary>Rejects oversized names.</summary>
    [Fact]
    public void Constructor_WithOversizedName_ThrowsDomainException() =>
        Assert.IsType<DomainException>(Record.Exception(() => new Workspace(Guid.NewGuid(), new string('a', Workspace.NameMaxLength + 1), DateTimeOffset.UtcNow)));
}
