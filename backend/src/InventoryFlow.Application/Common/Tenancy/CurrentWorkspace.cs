using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Common.Tenancy;

/// <summary>Represents the server-resolved workspace for the current request.</summary>
public sealed record CurrentWorkspace
{
    public CurrentWorkspace(Guid id, string name, WorkspaceMemberRole role)
    {
        ArgumentNullException.ThrowIfNull(name);
        (Id, Name, Role) = (id, name, role);
    }

    public Guid Id { get; }
    public string Name { get; }
    public WorkspaceMemberRole Role { get; }
}
