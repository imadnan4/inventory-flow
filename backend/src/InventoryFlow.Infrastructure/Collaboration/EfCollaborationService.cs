using System.Data;
using InventoryFlow.Application.Features.Collaboration;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Domain.Exceptions;
using InventoryFlow.Infrastructure.Authentication;
using InventoryFlow.Infrastructure.Identity;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InventoryFlow.Infrastructure.Collaboration;

/// <summary>Implements workspace collaboration operations with EF Core.</summary>
public sealed class EfCollaborationService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RefreshTokenGenerator tokenGenerator,
    TimeProvider timeProvider) : ICollaborationService
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<WorkspaceMemberResponse>> ListMembersAsync(ListWorkspaceMembersQuery query, CancellationToken cancellationToken)
    {
        EnsureOwner(query.CurrentUserRole);
        return await (from member in dbContext.WorkspaceMembers.AsNoTracking()
                      join user in dbContext.Users.AsNoTracking() on member.UserId equals user.Id
                      where member.WorkspaceId == query.WorkspaceId
                      orderby member.Role == WorkspaceMemberRole.Owner descending, user.DisplayName
                      select new WorkspaceMemberResponse(user.Id, user.Email ?? string.Empty, user.DisplayName, member.Role.ToString(), member.CreatedAtUtc)).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<WorkspaceInvitationResponse>> ListInvitationsAsync(ListWorkspaceInvitationsQuery query, CancellationToken cancellationToken)
    {
        EnsureOwner(query.CurrentUserRole);
        return await dbContext.WorkspaceInvitations.AsNoTracking()
            .Where(invitation => invitation.WorkspaceId == query.WorkspaceId)
            .OrderByDescending(invitation => invitation.CreatedAtUtc)
            .Select(invitation => ToResponse(invitation))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CreatedWorkspaceInvitationResponse> CreateInvitationAsync(CreateWorkspaceInvitationCommand command, CancellationToken cancellationToken)
    {
        EnsureOwner(command.CurrentUserRole);
        var email = command.Email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email)) throw new DomainException("Invitation email is required.");
        var normalizedEmail = userManager.NormalizeEmail(email);
        var invitedUser = await userManager.FindByEmailAsync(email);
        if (invitedUser is null) throw new DomainException("Invitation email must belong to an existing registered user.");
        if (await dbContext.WorkspaceMembers.AsNoTracking().AnyAsync(member => member.WorkspaceId == command.WorkspaceId && member.UserId == invitedUser.Id, cancellationToken))
            throw new CollaborationConflictException("The invited user is already a workspace member.");

        var token = tokenGenerator.Create();
        var now = timeProvider.GetUtcNow();
        var invitation = new WorkspaceInvitation(
            Guid.NewGuid(),
            command.WorkspaceId,
            normalizedEmail,
            WorkspaceMemberRole.Member,
            tokenGenerator.Hash(token),
            now.Add(InvitationLifetime),
            command.CreatedByUserId,
            now);
        dbContext.WorkspaceInvitations.Add(invitation);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraint(exception))
        {
            throw new CollaborationConflictException("A pending invitation already exists for this email address.");
        }
        return new CreatedWorkspaceInvitationResponse(ToResponse(invitation), token);
    }

    /// <inheritdoc />
    public async Task RevokeInvitationAsync(RevokeWorkspaceInvitationCommand command, CancellationToken cancellationToken)
    {
        EnsureOwner(command.CurrentUserRole);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            var now = timeProvider.GetUtcNow();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var invitation = await dbContext.WorkspaceInvitations.SingleOrDefaultAsync(item => item.Id == command.InvitationId && item.WorkspaceId == command.WorkspaceId, cancellationToken);
            if (invitation is null) throw new DomainException("Invitation was not found.");
            if (!invitation.IsPending(now)) throw new DomainException("Only pending invitations can be revoked.");
            invitation.Revoke(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<WorkspaceInvitationResponse> AcceptInvitationAsync(AcceptWorkspaceInvitationCommand command, CancellationToken cancellationToken)
    {
        var hash = tokenGenerator.Hash(command.Token);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var now = timeProvider.GetUtcNow();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var invitation = await dbContext.WorkspaceInvitations.SingleOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
            if (invitation is null || !invitation.IsPending(now)) throw new DomainException("Invitation is invalid or expired.");
            var user = await userManager.FindByIdAsync(command.UserId.ToString());
            if (user is null) throw new DomainException("Authenticated user was not found.");
            var normalizedEmail = userManager.NormalizeEmail(user.Email ?? string.Empty);
            if (!string.Equals(invitation.NormalizedEmail, normalizedEmail, StringComparison.Ordinal))
                throw new DomainException("Invitation email does not match the authenticated user.");
            if (await dbContext.WorkspaceMembers.AnyAsync(member => member.WorkspaceId == invitation.WorkspaceId && member.UserId == command.UserId, cancellationToken))
                throw new CollaborationConflictException("The authenticated user is already a workspace member.");

            dbContext.WorkspaceMembers.Add(new WorkspaceMember(Guid.NewGuid(), invitation.WorkspaceId, command.UserId, WorkspaceMemberRole.Member, now));
            invitation.Accept(command.UserId, now);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraint(exception))
            {
                throw new CollaborationConflictException("The authenticated user is already a workspace member.");
            }
            return ToResponse(invitation);
        });
    }

    private static void EnsureOwner(WorkspaceMemberRole role)
    {
        if (role != WorkspaceMemberRole.Owner) throw new UnauthorizedAccessException("Owner role is required.");
    }

    private static WorkspaceInvitationResponse ToResponse(WorkspaceInvitation invitation) => new(
        invitation.Id,
        invitation.NormalizedEmail,
        invitation.Role.ToString(),
        invitation.ExpiresAtUtc,
        invitation.CreatedAtUtc,
        invitation.CreatedByUserId,
        invitation.AcceptedAtUtc,
        invitation.AcceptedByUserId,
        invitation.RevokedAtUtc);

    private static bool IsUniqueConstraint(DbUpdateException exception) => exception.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true
        || exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true;
}
