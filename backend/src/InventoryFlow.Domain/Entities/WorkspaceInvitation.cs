using InventoryFlow.Domain.Common;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.Domain.Entities;

/// <summary>Represents a single-use invitation to join a workspace.</summary>
public sealed class WorkspaceInvitation : Entity<Guid>
{
    /// <summary>Maximum length for stored normalized invite email addresses.</summary>
    public const int NormalizedEmailMaxLength = 256;

    /// <summary>Maximum length for SHA-256 token hashes encoded as hexadecimal.</summary>
    public const int TokenHashMaxLength = 64;

    /// <summary>Initializes a workspace invitation.</summary>
    public WorkspaceInvitation(
        Guid id,
        Guid workspaceId,
        string normalizedEmail,
        WorkspaceMemberRole role,
        string tokenHash,
        DateTimeOffset expiresAtUtc,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || createdByUserId == Guid.Empty)
            throw new DomainException("Invitation, workspace, and creator identifiers are required.");
        if (role != WorkspaceMemberRole.Member)
            throw new DomainException("Only Member invitations are supported.");
        NormalizedEmail = NormalizeEmail(normalizedEmail);
        TokenHash = NormalizeTokenHash(tokenHash);
        if (createdAtUtc.Offset != TimeSpan.Zero || expiresAtUtc.Offset != TimeSpan.Zero)
            throw new DomainException("Invitation timestamps must be in UTC.");
        if (expiresAtUtc <= createdAtUtc)
            throw new DomainException("Invitation expiration must be after creation time.");

        WorkspaceId = workspaceId;
        Role = role;
        ExpiresAtUtc = expiresAtUtc;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Gets the workspace identifier.</summary>
    public Guid WorkspaceId { get; private set; }

    /// <summary>Gets the normalized invited email address.</summary>
    public string NormalizedEmail { get; private set; }

    /// <summary>Gets the invited role.</summary>
    public WorkspaceMemberRole Role { get; private set; }

    /// <summary>Gets the one-way hash of the invitation token.</summary>
    public string TokenHash { get; private set; }

    /// <summary>Gets the UTC instant at which the invitation expires.</summary>
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    /// <summary>Gets the user identifier that created the invitation.</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Gets the UTC creation instant.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Gets the UTC instant at which the invitation was accepted, if any.</summary>
    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    /// <summary>Gets the user identifier that accepted the invitation, if any.</summary>
    public Guid? AcceptedByUserId { get; private set; }

    /// <summary>Gets the UTC instant at which the invitation was revoked, if any.</summary>
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    /// <summary>Returns whether the invitation can be accepted at the supplied time.</summary>
    public bool IsPending(DateTimeOffset utcNow) =>
        AcceptedAtUtc is null && RevokedAtUtc is null && ExpiresAtUtc > utcNow;

    /// <summary>Marks the invitation as revoked.</summary>
    public void Revoke(DateTimeOffset revokedAtUtc)
    {
        if (revokedAtUtc.Offset != TimeSpan.Zero)
            throw new DomainException("Invitation revocation time must be in UTC.");
        if (AcceptedAtUtc is not null)
            throw new DomainException("Accepted invitations cannot be revoked.");
        if (RevokedAtUtc is not null)
            throw new DomainException("Invitation has already been revoked.");
        RevokedAtUtc = revokedAtUtc;
    }

    /// <summary>Marks the invitation as accepted by the supplied user.</summary>
    public void Accept(Guid acceptedByUserId, DateTimeOffset acceptedAtUtc)
    {
        if (acceptedByUserId == Guid.Empty)
            throw new DomainException("Invitation accepter identifier is required.");
        if (acceptedAtUtc.Offset != TimeSpan.Zero)
            throw new DomainException("Invitation acceptance time must be in UTC.");
        if (!IsPending(acceptedAtUtc))
            throw new DomainException("Invitation is not pending.");
        AcceptedByUserId = acceptedByUserId;
        AcceptedAtUtc = acceptedAtUtc;
    }

    private static string NormalizeEmail(string normalizedEmail)
    {
        var value = normalizedEmail?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > NormalizedEmailMaxLength)
            throw new DomainException($"Invitation email must contain between 1 and {NormalizedEmailMaxLength} characters.");
        return value;
    }

    private static string NormalizeTokenHash(string tokenHash)
    {
        var value = tokenHash?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > TokenHashMaxLength)
            throw new DomainException("Invitation token hash is required.");
        return value;
    }
}
