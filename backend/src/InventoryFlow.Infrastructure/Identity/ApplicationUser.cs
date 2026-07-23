using Microsoft.AspNetCore.Identity;

namespace InventoryFlow.Infrastructure.Identity;

/// <summary>
/// Represents an authenticated Inventory Flow user.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Gets or sets the user's display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}
