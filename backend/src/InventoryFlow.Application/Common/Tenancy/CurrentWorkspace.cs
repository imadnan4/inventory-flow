using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Common.Tenancy;

/// <summary>Represents the server-resolved workspace for the current request.</summary>
public sealed record CurrentWorkspace(Guid Id, string Name, WorkspaceMemberRole Role);
