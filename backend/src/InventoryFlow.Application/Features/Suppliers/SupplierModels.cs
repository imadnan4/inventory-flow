using InventoryFlow.Domain.Entities;
using MediatR;

namespace InventoryFlow.Application.Features.Suppliers;

/// <summary>Supplies browser input for supplier creation.</summary>
public sealed record CreateSupplierRequest(string Name);
/// <summary>Represents a supplier exposed to clients.</summary>
public sealed record SupplierResponse(Guid Id, string Name, DateTimeOffset CreatedAtUtc)
{
    /// <summary>Maps a domain supplier to a response.</summary>
    public static SupplierResponse From(Supplier supplier) => new(supplier.Id, supplier.Name, supplier.CreatedAtUtc);
}
/// <summary>Creates a supplier in a server-resolved workspace.</summary>
public sealed record CreateSupplierCommand(Guid WorkspaceId, string Name) : IRequest<SupplierResponse>;
/// <summary>Lists active suppliers in a server-resolved workspace.</summary>
public sealed record ListSuppliersQuery(Guid WorkspaceId) : IRequest<IReadOnlyList<SupplierResponse>>;
/// <summary>Archives a supplier in a server-resolved workspace.</summary>
public sealed record ArchiveSupplierCommand(Guid WorkspaceId, Guid SupplierId) : IRequest<bool>;
