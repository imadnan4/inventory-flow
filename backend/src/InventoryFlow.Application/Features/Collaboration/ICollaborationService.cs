namespace InventoryFlow.Application.Features.Collaboration;

/// <summary>Defines workspace collaboration operations.</summary>
public interface ICollaborationService
{
    /// <summary>Lists active workspace members.</summary>
    Task<IReadOnlyCollection<WorkspaceMemberResponse>> ListMembersAsync(ListWorkspaceMembersQuery query, CancellationToken cancellationToken);

    /// <summary>Lists active workspace invitations.</summary>
    Task<IReadOnlyCollection<WorkspaceInvitationResponse>> ListInvitationsAsync(ListWorkspaceInvitationsQuery query, CancellationToken cancellationToken);

    /// <summary>Creates a member invitation.</summary>
    Task<CreatedWorkspaceInvitationResponse> CreateInvitationAsync(CreateWorkspaceInvitationCommand command, CancellationToken cancellationToken);

    /// <summary>Revokes a pending invitation.</summary>
    Task RevokeInvitationAsync(RevokeWorkspaceInvitationCommand command, CancellationToken cancellationToken);

    /// <summary>Accepts an invitation.</summary>
    Task<WorkspaceInvitationResponse> AcceptInvitationAsync(AcceptWorkspaceInvitationCommand command, CancellationToken cancellationToken);
}

/// <summary>Represents a collaboration conflict.</summary>
public sealed class CollaborationConflictException(string message) : Exception(message);
