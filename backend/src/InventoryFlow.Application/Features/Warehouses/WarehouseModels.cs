using InventoryFlow.Domain.Entities;
using MediatR;
namespace InventoryFlow.Application.Features.Warehouses;
public sealed record CreateWarehouseRequest(string Name); public sealed record WarehouseResponse(Guid Id, string Name, DateTimeOffset CreatedAtUtc) { public static WarehouseResponse From(Warehouse x) => new(x.Id, x.Name, x.CreatedAtUtc); }
public sealed record CreateWarehouseCommand(Guid WorkspaceId, string Name) : IRequest<WarehouseResponse>; public sealed record ListWarehousesQuery(Guid WorkspaceId) : IRequest<IReadOnlyList<WarehouseResponse>>; public sealed record ArchiveWarehouseCommand(Guid WorkspaceId, Guid WarehouseId) : IRequest<bool>;
