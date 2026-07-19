using InventoryFlow.Domain.Entities;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.UnitTests.Domain;

/// <summary>Verifies workspace invitation invariants and lifecycle.</summary>
public sealed class WorkspaceInvitationTests
{
    /// <summary>Creates a pending Member invitation.</summary>
    [Fact]
    public void Constructor_WithMember_CreatesPendingInvitation()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = CreateInvitation(now);

        Assert.Equal(WorkspaceMemberRole.Member, invitation.Role);
        Assert.True(invitation.IsPending(now));
    }

    /// <summary>Rejects Owner invitations in the P0 collaboration slice.</summary>
    [Fact]
    public void Constructor_WithOwnerRole_ThrowsDomainException()
    {
        var now = DateTimeOffset.UtcNow;
        var exception = Record.Exception(() => new WorkspaceInvitation(Guid.NewGuid(), Guid.NewGuid(), "USER@EXAMPLE.TEST", WorkspaceMemberRole.Owner, "hash", now.AddDays(1), Guid.NewGuid(), now));

        Assert.IsType<DomainException>(exception);
    }

    /// <summary>Marks a pending invitation as accepted.</summary>
    [Fact]
    public void Accept_WithPendingInvitation_MarksAccepted()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = CreateInvitation(now);
        var userId = Guid.NewGuid();

        invitation.Accept(userId, now.AddMinutes(1));

        Assert.Equal(userId, invitation.AcceptedByUserId);
        Assert.NotNull(invitation.AcceptedAtUtc);
        Assert.False(invitation.IsPending(now.AddMinutes(1)));
    }

    /// <summary>Marks a pending invitation as revoked.</summary>
    [Fact]
    public void Revoke_WithPendingInvitation_MarksRevoked()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = CreateInvitation(now);

        invitation.Revoke(now.AddMinutes(1));

        Assert.NotNull(invitation.RevokedAtUtc);
        Assert.False(invitation.IsPending(now.AddMinutes(1)));
    }

    private static WorkspaceInvitation CreateInvitation(DateTimeOffset now) => new(Guid.NewGuid(), Guid.NewGuid(), "USER@EXAMPLE.TEST", WorkspaceMemberRole.Member, "hash", now.AddDays(1), Guid.NewGuid(), now);
}
