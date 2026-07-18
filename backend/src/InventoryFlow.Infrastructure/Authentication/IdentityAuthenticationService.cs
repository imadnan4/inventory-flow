using System.Data;
using InventoryFlow.Application.Features.Authentication;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Identity;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InventoryFlow.Infrastructure.Authentication;

/// <summary>Implements identity, workspace provisioning, session issuance, and refresh rotation.</summary>
public sealed class IdentityAuthenticationService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext dbContext,
    JwtAccessTokenIssuer accessTokenIssuer,
    RefreshTokenGenerator refreshTokenGenerator,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : IAuthenticationService
{
    private readonly JwtOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<AuthenticationSession> RegisterAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var now = timeProvider.GetUtcNow();
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = command.Email.Trim(), Email = command.Email.Trim(), DisplayName = command.DisplayName.Trim() };
            var result = await userManager.CreateAsync(user, command.Password);
            if (!result.Succeeded) throw new AuthenticationException();
            var workspace = new Workspace(Guid.NewGuid(), CreatePersonalWorkspaceName(user.DisplayName), now);
            var membership = new WorkspaceMember(Guid.NewGuid(), workspace.Id, user.Id, WorkspaceMemberRole.Owner, now);
            dbContext.AddRange(workspace, membership);
            var session = CreateSession(user, ToWorkspace(workspace), Guid.NewGuid(), now);
            dbContext.RefreshTokens.Add(session.Token);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return session.Session;
        });
    }

    /// <inheritdoc />
    public async Task<AuthenticationSession?> LoginAsync(LoginUserCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(command.Email.Trim());
        if (user is null) return null;
        var result = await signInManager.CheckPasswordSignInAsync(user, command.Password, lockoutOnFailure: true);
        if (!result.Succeeded) return null;
        return await IssueSessionAsync(user, Guid.NewGuid(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AuthenticationSession?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = refreshTokenGenerator.Hash(refreshToken);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var now = timeProvider.GetUtcNow();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var token = await dbContext.RefreshTokens.SingleOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
            if (token is null) { await transaction.CommitAsync(cancellationToken); return null; }
            if (!token.IsActive(now))
            {
                await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }
            token.Revoke(now);
            var user = await userManager.FindByIdAsync(token.UserId.ToString());
            var workspace = user is null ? null : await GetWorkspaceAsync(user.Id, cancellationToken);
            if (user is null || workspace is null)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }
            var session = CreateSession(user, workspace, token.FamilyId, now);
            dbContext.RefreshTokens.Add(session.Token);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return session.Session;
        });
    }

    /// <inheritdoc />
    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var hash = refreshTokenGenerator.Hash(refreshToken);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            var now = timeProvider.GetUtcNow();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var token = await dbContext.RefreshTokens.SingleOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
            if (token is not null) { await RevokeFamilyAsync(token.FamilyId, now, cancellationToken); await dbContext.SaveChangesAsync(cancellationToken); }
            await transaction.CommitAsync(cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<AuthenticatedUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        var workspace = user is null ? null : await GetWorkspaceAsync(user.Id, cancellationToken);
        return user is null || workspace is null ? null : ToUser(user, workspace);
    }

    private async Task<AuthenticationSession?> IssueSessionAsync(ApplicationUser user, Guid familyId, CancellationToken cancellationToken)
    {
        var workspace = await GetWorkspaceAsync(user.Id, cancellationToken);
        if (workspace is null) return null;
        var created = CreateSession(user, workspace, familyId, timeProvider.GetUtcNow());
        dbContext.RefreshTokens.Add(created.Token);
        await dbContext.SaveChangesAsync(cancellationToken);
        return created.Session;
    }

    private async Task<AuthenticatedWorkspace?> GetWorkspaceAsync(Guid userId, CancellationToken cancellationToken)
    {
        var workspaces = await (from member in dbContext.WorkspaceMembers.AsNoTracking()
                                join workspace in dbContext.Workspaces.AsNoTracking() on member.WorkspaceId equals workspace.Id
                                where member.UserId == userId && member.Role == WorkspaceMemberRole.Owner
                                select new AuthenticatedWorkspace(workspace.Id, workspace.Name)).Take(2).ToListAsync(cancellationToken);
        return workspaces.Count == 1 ? workspaces[0] : null;
    }

    private (AuthenticationSession Session, RefreshToken Token) CreateSession(ApplicationUser user, AuthenticatedWorkspace workspace, Guid familyId, DateTimeOffset now)
    {
        var refreshValue = refreshTokenGenerator.Create();
        var token = new RefreshToken(Guid.NewGuid(), user.Id, familyId, refreshTokenGenerator.Hash(refreshValue), now.AddDays(_options.RefreshTokenLifetimeDays));
        var access = accessTokenIssuer.Issue(ToUser(user, workspace));
        return (new AuthenticationSession(new AuthenticationResponse(access.Token, access.ExpiresAtUtc, ToUser(user, workspace)), refreshValue), token);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.RefreshTokens.Where(item => item.FamilyId == familyId && item.RevokedAtUtc == null && item.ExpiresAtUtc > now).ToListAsync(cancellationToken);
        foreach (var token in activeTokens) token.Revoke(now);
    }

    private static string CreatePersonalWorkspaceName(string displayName)
    {
        const string suffix = "'s workspace";
        return string.Concat(displayName.AsSpan(0, Math.Min(displayName.Length, Workspace.NameMaxLength - suffix.Length)), suffix);
    }

    private static AuthenticatedWorkspace ToWorkspace(Workspace workspace) => new(workspace.Id, workspace.Name);
    private static AuthenticatedUser ToUser(ApplicationUser user, AuthenticatedWorkspace workspace) => new(user.Id, user.Email ?? string.Empty, user.DisplayName, workspace);
}
