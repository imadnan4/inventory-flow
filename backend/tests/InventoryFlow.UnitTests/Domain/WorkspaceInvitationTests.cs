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

    /// <summary>Rejects empty or whitespace normalized email.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyOrWhitespaceEmail_ThrowsDomainException(string email)
    {
        var now = DateTimeOffset.UtcNow;
        var exception = Record.Exception(() => new WorkspaceInvitation(Guid.NewGuid(), Guid.NewGuid(), email, WorkspaceMemberRole.Member, "hash", now.AddDays(1), Guid.NewGuid(), now));

        Assert.IsType<DomainException>(exception);
    }

    /// <summary>Rejects emails exceeding the normalized max length.</summary>
    [Fact]
    public void Constructor_WithEmailExceedingMaxLength_ThrowsDomainException()
    {
        var now = DateTimeOffset.UtcNow;
        var tooLong = new string('a', WorkspaceInvitation.NormalizedEmailMaxLength + 1);
        var exception = Record.Exception(() => new WorkspaceInvitation(Guid.NewGuid(), Guid.NewGuid(), tooLong, WorkspaceMemberRole.Member, "hash", now.AddDays(1), Guid.NewGuid(), now));

        Assert.IsType<DomainException>(exception);
    }

    /// <summary>Rejects empty or whitespace token hash.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyOrWhitespaceTokenHash_ThrowsDomainException(string tokenHash)
    {
        var now = DateTimeOffset.UtcNow;
        var exception = Record.Exception(() => new WorkspaceInvitation(Guid.NewGuid(), Guid.NewGuid(), "USER@EXAMPLE.TEST", WorkspaceMemberRole.Member, tokenHash, now.AddDays(1), Guid.NewGuid(), now));

        Assert.IsType<DomainException>(exception);
    }

    /// <summary>Rejects token hashes exceeding the max length.</summary>
    [Fact]
    public void Constructor_WithTokenHashExceedingMaxLength_ThrowsDomainException()
    {
        var now = DateTimeOffset.UtcNow;
        var tooLong = new string('a', WorkspaceInvitation.TokenHashMaxLength + 1);
        var exception = Record.Exception(() => new WorkspaceInvitation(Guid.NewGuid(), Guid.NewGuid(), "USER@EXAMPLE.TEST", WorkspaceMemberRole.Member, tooLong, now.AddDays(1), Guid.NewGuid(), now));

        Assert.IsType<DomainException>(exception);
    }

    /// <summary>Rejects non-UTC creation or expiration timestamps.</summary>
    [Theory]
    [InlineData(-5)]
    [InlineData(330)]
    public void Constructor_WithNonUtcTimestamps_ThrowsDomainException(int offsetMinutes)
    {
        var offset = TimeSpan.FromMinutes(offsetMinutes);
        var created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, offset);
        var expires = new DateTimeOffset(2026, 1, 2, 0, 0, 0, offset);
        var exception = Record.Exception(() => new WorkspaceInvitation(Guid.NewGuid(), Guid.NewGuid(), "USER@EXAMPLE.TEST", WorkspaceMemberRole.Member, "hash", expires, Guid.NewGuid(), created));

        Assert.IsType<DomainException>(exception);
    }

    /// <summary>Rejects expiration that is not strictly after creation.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithExpirationNotAfterCreation_ThrowsDomainException(int deltaMinutes)
    {
        var now = DateTimeOffset.UtcNow;
        var exception = Record.Exception(() => new WorkspaceInvitation(Guid.NewGuid(), Guid.NewGuid(), "USER@EXAMPLE.TEST", WorkspaceMemberRole.Member, "hash", now.AddMinutes(deltaMinutes), Guid.NewGuid(), now));

        Assert.IsType<DomainException>(exception);
    }

    /// <summary>Accept throws when the invitation is already accepted.</summary>
    [Fact]
    public void Accept_WhenAlreadyAccepted_ThrowsDomainException()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = CreateInvitation(now);
        invitation.Accept(Guid.NewGuid(), now.AddMinutes(1));

        var exception = Record.Exception(() => invitation.Accept(Guid.NewGuid(), now.AddMinutes(2)));

        Assert.IsType<DomainException>(exception);
    }

    /// <summary>Revoke throws when the invitation is already accepted.</summary>
    [Fact]
    public void Revoke_WhenAlreadyAccepted_ThrowsDomainException()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = CreateInvitation(now);
        invitation.Accept(Guid.NewGuid(), now.AddMinutes(1));

        var exception = Record.Exception(() => invitation.Revoke(now.AddMinutes(2)));

        Assert.IsType<DomainException>(exception);
    }

    /// <summary>Revoke throws when the invitation is already revoked.</summary>
    [Fact]
    public void Revoke_WhenAlreadyRevoked_ThrowsDomainException()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = CreateInvitation(now);
        invitation.Revoke(now.AddMinutes(1));

        var exception = Record.Exception(() => invitation.Revoke(now.AddMinutes(2)));

        Assert.IsType<DomainException>(exception);
    }
}
