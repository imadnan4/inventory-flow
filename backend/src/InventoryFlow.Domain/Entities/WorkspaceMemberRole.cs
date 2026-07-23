namespace InventoryFlow.Domain.Entities;

/// <summary>Defines the supported workspace membership roles.</summary>
public enum WorkspaceMemberRole
{
    /// <summary>Owns and administers a workspace.</summary>
    Owner = 1,

    /// <summary>Can perform operational work in a workspace.</summary>
    Member = 2,
}
