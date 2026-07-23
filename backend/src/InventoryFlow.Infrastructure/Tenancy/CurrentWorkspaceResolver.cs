using System.Security.Claims;
using InventoryFlow.Application.Common.Tenancy;
using InventoryFlow.Infrastructure.Authentication;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace InventoryFlow.Infrastructure.Tenancy;

/// <summary>Resolves a request workspace exclusively from authenticated membership data.</summary>
public sealed class CurrentWorkspaceResolver(IHttpContextAccessor httpContextAccessor, ApplicationDbContext dbContext) : ICurrentWorkspace
{
    /// <inheritdoc />
    public async Task<CurrentWorkspace?> GetAsync(CancellationToken cancellationToken = default)
    {
        var userIdValue = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var workspaceIdValue = httpContextAccessor.HttpContext?.User.FindFirstValue(JwtAccessTokenIssuer.WorkspaceIdClaimType);
        if (!Guid.TryParse(userIdValue, out var userId) || !Guid.TryParse(workspaceIdValue, out var workspaceId)) return null;
        var matches = await (from member in dbContext.WorkspaceMembers.AsNoTracking()
                             join workspace in dbContext.Workspaces.AsNoTracking() on member.WorkspaceId equals workspace.Id
                             where member.UserId == userId && member.WorkspaceId == workspaceId
                             select new CurrentWorkspace(workspace.Id, workspace.Name, member.Role)).Take(2).ToListAsync(cancellationToken);
        return matches.Count == 1 ? matches[0] : null;
    }
}
