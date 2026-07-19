using InventoryFlow.Domain.Entities;
using MediatR;

namespace InventoryFlow.Application.Features.Collaboration;

/// <summary>Represents a workspace member returned to administrators.</summary>
public sealed record WorkspaceMemberResponse(Guid UserId, string Email, string DisplayName, string Role, DateTimeOffset CreatedAtUtc);

/// <summary>Represents a workspace invitation returned to administrators.</summary>
public sealed record WorkspaceInvitationResponse(
    Guid Id,
    string Email,
    string Role,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset CreatedAtUtc,
    Guid CreatedByUserId,
    DateTimeOffset? AcceptedAtUtc,
    Guid? AcceptedByUserId,
    DateTimeOffset? RevokedAtUtc);

/// <summary>Represents a newly-created invitation with its plaintext token shown once.</summary>
public sealed record CreatedWorkspaceInvitationResponse(WorkspaceInvitationResponse Invitation, string Token);

/// <summary>Represents a request to create an invitation.</summary>
public sealed record CreateWorkspaceInvitationRequest(string Email);

/// <summary>Represents a request to accept an invitation.</summary>
public sealed record AcceptWorkspaceInvitationRequest(string Token);

/// <summary>Lists members for the active workspace.</summary>
public sealed record ListWorkspaceMembersQuery(Guid WorkspaceId, WorkspaceMemberRole CurrentUserRole) : IRequest<IReadOnlyCollection<WorkspaceMemberResponse>>;

/// <summary>Lists invitations for the active workspace.</summary>
public sealed record ListWorkspaceInvitationsQuery(Guid WorkspaceId, WorkspaceMemberRole CurrentUserRole) : IRequest<IReadOnlyCollection<WorkspaceInvitationResponse>>;

/// <summary>Creates a member invitation for the active workspace.</summary>
public sealed record CreateWorkspaceInvitationCommand(Guid WorkspaceId, Guid CreatedByUserId, WorkspaceMemberRole CurrentUserRole, string Email) : IRequest<CreatedWorkspaceInvitationResponse>;

/// <summary>Revokes a pending invitation for the active workspace.</summary>
public sealed record RevokeWorkspaceInvitationCommand(Guid WorkspaceId, Guid InvitationId, WorkspaceMemberRole CurrentUserRole) : IRequest;

/// <summary>Accepts an invitation for the authenticated user.</summary>
public sealed record AcceptWorkspaceInvitationCommand(Guid UserId, string Token) : IRequest<WorkspaceInvitationResponse>;
