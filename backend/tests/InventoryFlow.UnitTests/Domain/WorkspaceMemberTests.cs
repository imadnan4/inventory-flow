using InventoryFlow.Domain.Entities;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.UnitTests.Domain;

/// <summary>Verifies workspace membership invariants.</summary>
public sealed class WorkspaceMemberTests
{
    /// <summary>Creates the initial Owner membership.</summary>
    [Fact]
    public void Constructor_WithOwner_CreatesMembership()
    {
        var member = new WorkspaceMember(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), WorkspaceMemberRole.Owner, DateTimeOffset.UtcNow);
        Assert.Equal(WorkspaceMemberRole.Owner, member.Role);
    }

    /// <summary>Rejects an undefined role.</summary>
    [Fact]
    public void Constructor_WithUnsupportedRole_ThrowsDomainException() =>
        Assert.IsType<DomainException>(Record.Exception(() => new WorkspaceMember(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), (WorkspaceMemberRole)99, DateTimeOffset.UtcNow)));
}
