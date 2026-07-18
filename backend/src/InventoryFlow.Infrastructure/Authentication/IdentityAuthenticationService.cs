using InventoryFlow.Application.Features.Authentication;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Identity;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;

namespace InventoryFlow.Infrastructure.Authentication;

/// <summary>Implements identity, session issuance, and refresh rotation.</summary>
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
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = command.Email.Trim(), Email = command.Email.Trim(), DisplayName = command.DisplayName.Trim() };
        var result = await userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded) throw new AuthenticationException();
        return await IssueSessionAsync(user, Guid.NewGuid(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AuthenticationSession?> LoginAsync(LoginUserCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(command.Email.Trim());
        if (user is null) return null;
        var result = await signInManager.CheckPasswordSignInAsync(user, command.Password, lockoutOnFailure: true);
        return result.Succeeded ? await IssueSessionAsync(user, Guid.NewGuid(), cancellationToken) : null;
    }

    /// <inheritdoc />
    public async Task<AuthenticationSession?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var hash = refreshTokenGenerator.Hash(refreshToken);
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var token = await dbContext.RefreshTokens.SingleOrDefaultAsync(
                item => item.TokenHash == hash,
                cancellationToken);

            if (token is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            if (!token.IsActive(now))
            {
                await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            token.Revoke(now);
            var user = await userManager.FindByIdAsync(token.UserId.ToString());
            if (user is null)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            var session = CreateSession(user, token.FamilyId, now);
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
        var now = timeProvider.GetUtcNow();
        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var token = await dbContext.RefreshTokens.SingleOrDefaultAsync(
                item => item.TokenHash == hash,
                cancellationToken);

            if (token is not null)
            {
                await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<AuthenticatedUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null ? null : ToUser(user);
    }

    private async Task<AuthenticationSession> IssueSessionAsync(ApplicationUser user, Guid familyId, CancellationToken cancellationToken)
    {
        var created = CreateSession(user, familyId, timeProvider.GetUtcNow());
        dbContext.RefreshTokens.Add(created.Token);
        await dbContext.SaveChangesAsync(cancellationToken);
        return created.Session;
    }

    private (AuthenticationSession Session, RefreshToken Token) CreateSession(ApplicationUser user, Guid familyId, DateTimeOffset now)
    {
        var refreshValue = refreshTokenGenerator.Create();
        var token = new RefreshToken(Guid.NewGuid(), user.Id, familyId, refreshTokenGenerator.Hash(refreshValue), now.AddDays(_options.RefreshTokenLifetimeDays));
        var access = accessTokenIssuer.Issue(ToUser(user));
        return (new AuthenticationSession(new AuthenticationResponse(access.Token, access.ExpiresAtUtc, ToUser(user)), refreshValue), token);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.RefreshTokens.Where(item => item.FamilyId == familyId && item.RevokedAtUtc == null && item.ExpiresAtUtc > now).ToListAsync(cancellationToken);
        foreach (var token in activeTokens) token.Revoke(now);
    }

    private static AuthenticatedUser ToUser(ApplicationUser user) => new(user.Id, user.Email ?? string.Empty, user.DisplayName);
}
