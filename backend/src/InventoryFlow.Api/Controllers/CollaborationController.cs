using System.Security.Claims;
using InventoryFlow.Application.Common.Tenancy;
using InventoryFlow.Application.Features.Collaboration;
using InventoryFlow.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryFlow.Api.Controllers;

/// <summary>Provides workspace collaboration endpoints.</summary>
[ApiController]
[Authorize]
[Route("api/collaboration")]
public sealed class CollaborationController(ISender sender, ICurrentWorkspace currentWorkspace) : ControllerBase
{
    /// <summary>Lists members in the active workspace. Owner-only.</summary>
    [HttpGet("members")]
    [ProducesResponseType<IReadOnlyCollection<WorkspaceMemberResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<WorkspaceMemberResponse>>> ListMembers(CancellationToken cancellationToken)
    {
        var workspace = await ResolveWorkspaceAsync(cancellationToken);
        if (workspace is null) return Forbid();
        if (workspace.Role != WorkspaceMemberRole.Owner) return Forbid();
        return Ok(await sender.Send(new ListWorkspaceMembersQuery(workspace.Id, workspace.Role), cancellationToken));
    }

    /// <summary>Lists invitations in the active workspace. Owner-only.</summary>
    [HttpGet("invitations")]
    [ProducesResponseType<IReadOnlyCollection<WorkspaceInvitationResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<WorkspaceInvitationResponse>>> ListInvitations(CancellationToken cancellationToken)
    {
        var workspace = await ResolveWorkspaceAsync(cancellationToken);
        if (workspace is null) return Forbid();
        if (workspace.Role != WorkspaceMemberRole.Owner) return Forbid();
        return Ok(await sender.Send(new ListWorkspaceInvitationsQuery(workspace.Id, workspace.Role), cancellationToken));
    }

    /// <summary>Creates a Member invitation for an existing registered email. Owner-only.</summary>
    [HttpPost("invitations")]
    [ProducesResponseType<CreatedWorkspaceInvitationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CreatedWorkspaceInvitationResponse>> CreateInvitation(CreateWorkspaceInvitationRequest request, CancellationToken cancellationToken)
    {
        var workspace = await ResolveWorkspaceAsync(cancellationToken);
        if (workspace is null) return Forbid();
        if (workspace.Role != WorkspaceMemberRole.Owner) return Forbid();
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();
        return Ok(await sender.Send(new CreateWorkspaceInvitationCommand(workspace.Id, userId.Value, workspace.Role, request.Email), cancellationToken));
    }

    /// <summary>Revokes a pending invitation. Owner-only.</summary>
    [HttpPost("invitations/{invitationId:guid}/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeInvitation(Guid invitationId, CancellationToken cancellationToken)
    {
        var workspace = await ResolveWorkspaceAsync(cancellationToken);
        if (workspace is null) return Forbid();
        if (workspace.Role != WorkspaceMemberRole.Owner) return Forbid();
        await sender.Send(new RevokeWorkspaceInvitationCommand(workspace.Id, invitationId, workspace.Role), cancellationToken);
        return NoContent();
    }

    /// <summary>Accepts an invitation as the authenticated registered user.</summary>
    [HttpPost("invitations/accept")]
    [ProducesResponseType<WorkspaceInvitationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkspaceInvitationResponse>> AcceptInvitation(AcceptWorkspaceInvitationRequest request, CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();
        return Ok(await sender.Send(new AcceptWorkspaceInvitationCommand(userId.Value, request.Token), cancellationToken));
    }

    private async Task<CurrentWorkspace?> ResolveWorkspaceAsync(CancellationToken cancellationToken) => await currentWorkspace.GetAsync(cancellationToken);

    private Guid? GetAuthenticatedUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
