using InventoryFlow.Application.Common.Tenancy;
using InventoryFlow.Application.Features.Suppliers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryFlow.Api.Controllers;

/// <summary>Provides workspace-scoped supplier catalog endpoints.</summary>
[ApiController]
[Authorize]
[Route("api/suppliers")]
public sealed class SuppliersController(ISender sender, ICurrentWorkspace currentWorkspace) : ControllerBase
{
    /// <summary>Creates a supplier in the current workspace.</summary>
    [HttpPost]
    [ProducesResponseType<SupplierResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SupplierResponse>> Create(CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        var workspace = await currentWorkspace.GetAsync(cancellationToken);
        if (workspace is null) return Forbid();
        var supplier = await sender.Send(new CreateSupplierCommand(workspace.Id, request.Name), cancellationToken);
        return Created($"/api/suppliers/{supplier.Id}", supplier);
    }

    /// <summary>Lists active suppliers in the current workspace.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SupplierResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<SupplierResponse>>> List(CancellationToken cancellationToken)
    {
        var workspace = await currentWorkspace.GetAsync(cancellationToken);
        if (workspace is null) return Forbid();
        return Ok(await sender.Send(new ListSuppliersQuery(workspace.Id), cancellationToken));
    }

    /// <summary>Idempotently archives a supplier.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var workspace = await currentWorkspace.GetAsync(cancellationToken);
        if (workspace is null) return Forbid();
        return await sender.Send(new ArchiveSupplierCommand(workspace.Id, id), cancellationToken) ? NoContent() : NotFound();
    }
}
