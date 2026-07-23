using InventoryFlow.Application.Common.Tenancy;
using InventoryFlow.Application.Features.Warehouses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryFlow.Api.Controllers;

/// <summary>Provides workspace-scoped warehouse catalog endpoints.</summary>
[ApiController]
[Authorize]
[Route("api/warehouses")]
public sealed class WarehousesController(ISender sender, ICurrentWorkspace currentWorkspace) : ControllerBase
{
    /// <summary>Creates a warehouse in the current workspace.</summary>
    [HttpPost]
    [ProducesResponseType<WarehouseResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<WarehouseResponse>> Create(CreateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var workspace = await currentWorkspace.GetAsync(cancellationToken);
        if (workspace is null) return Forbid();
        var warehouse = await sender.Send(new CreateWarehouseCommand(workspace.Id, request.Name), cancellationToken);
        return Created($"/api/warehouses/{warehouse.Id}", warehouse);
    }

    /// <summary>Lists active warehouses in the current workspace.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<WarehouseResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<WarehouseResponse>>> List(CancellationToken cancellationToken)
    {
        var workspace = await currentWorkspace.GetAsync(cancellationToken);
        if (workspace is null) return Forbid();
        return Ok(await sender.Send(new ListWarehousesQuery(workspace.Id), cancellationToken));
    }

    /// <summary>Idempotently archives a warehouse with no stock on hand.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var workspace = await currentWorkspace.GetAsync(cancellationToken);
        if (workspace is null) return Forbid();
        return await sender.Send(new ArchiveWarehouseCommand(workspace.Id, id), cancellationToken) ? NoContent() : NotFound();
    }
}
