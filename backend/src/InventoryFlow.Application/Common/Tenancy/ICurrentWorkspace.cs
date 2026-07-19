namespace InventoryFlow.Application.Common.Tenancy;

/// <summary>Resolves the authenticated request's unambiguous current workspace.</summary>
public interface ICurrentWorkspace
{
    /// <summary>Returns the current workspace or null when unauthenticated, missing, or ambiguous.</summary>
    Task<CurrentWorkspace?> GetAsync(CancellationToken cancellationToken = default);
}
