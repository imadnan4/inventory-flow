using InventoryFlow.Domain.Common;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.Domain.Entities;

/// <summary>Represents a user's membership in a workspace.</summary>
public sealed class WorkspaceMember : Entity<Guid>
{
    /// <summary>Initializes a workspace member.</summary>
    public WorkspaceMember(Guid id, Guid workspaceId, Guid userId, WorkspaceMemberRole role, DateTimeOffset createdAtUtc) : base(id)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || userId == Guid.Empty)
            throw new DomainException("Workspace member, workspace, and user identifiers are required.");
        if (!Enum.IsDefined(role)) throw new DomainException("Workspace membership role is not supported.");
        if (createdAtUtc.Offset != TimeSpan.Zero) throw new DomainException("Workspace membership creation time must be in UTC.");
        WorkspaceId = workspaceId;
        UserId = userId;
        Role = role;
        CreatedAtUtc = createdAtUtc;
    }
    /// <summary>Gets the workspace identifier.</summary>
    public Guid WorkspaceId { get; private set; }
    /// <summary>Gets the Identity user identifier.</summary>
    public Guid UserId { get; private set; }
    /// <summary>Gets the membership role.</summary>
    public WorkspaceMemberRole Role { get; private set; }
    /// <summary>Gets the UTC creation instant.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
