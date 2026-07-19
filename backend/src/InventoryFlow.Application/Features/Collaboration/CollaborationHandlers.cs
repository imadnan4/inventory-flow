using MediatR;

namespace InventoryFlow.Application.Features.Collaboration;

/// <summary>Delegates member listing to the collaboration port.</summary>
public sealed class ListWorkspaceMembersHandler(ICollaborationService service) : IRequestHandler<ListWorkspaceMembersQuery, IReadOnlyCollection<WorkspaceMemberResponse>>
{ public Task<IReadOnlyCollection<WorkspaceMemberResponse>> Handle(ListWorkspaceMembersQuery request, CancellationToken cancellationToken) => service.ListMembersAsync(request, cancellationToken); }

/// <summary>Delegates invitation listing to the collaboration port.</summary>
public sealed class ListWorkspaceInvitationsHandler(ICollaborationService service) : IRequestHandler<ListWorkspaceInvitationsQuery, IReadOnlyCollection<WorkspaceInvitationResponse>>
{ public Task<IReadOnlyCollection<WorkspaceInvitationResponse>> Handle(ListWorkspaceInvitationsQuery request, CancellationToken cancellationToken) => service.ListInvitationsAsync(request, cancellationToken); }

/// <summary>Delegates invitation creation to the collaboration port.</summary>
public sealed class CreateWorkspaceInvitationHandler(ICollaborationService service) : IRequestHandler<CreateWorkspaceInvitationCommand, CreatedWorkspaceInvitationResponse>
{ public Task<CreatedWorkspaceInvitationResponse> Handle(CreateWorkspaceInvitationCommand request, CancellationToken cancellationToken) => service.CreateInvitationAsync(request, cancellationToken); }

/// <summary>Delegates invitation revocation to the collaboration port.</summary>
public sealed class RevokeWorkspaceInvitationHandler(ICollaborationService service) : IRequestHandler<RevokeWorkspaceInvitationCommand>
{ public Task Handle(RevokeWorkspaceInvitationCommand request, CancellationToken cancellationToken) => service.RevokeInvitationAsync(request, cancellationToken); }

/// <summary>Delegates invitation acceptance to the collaboration port.</summary>
public sealed class AcceptWorkspaceInvitationHandler(ICollaborationService service) : IRequestHandler<AcceptWorkspaceInvitationCommand, WorkspaceInvitationResponse>
{ public Task<WorkspaceInvitationResponse> Handle(AcceptWorkspaceInvitationCommand request, CancellationToken cancellationToken) => service.AcceptInvitationAsync(request, cancellationToken); }
