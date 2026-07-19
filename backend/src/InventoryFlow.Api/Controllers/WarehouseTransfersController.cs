using InventoryFlow.Application.Common.Tenancy;
using InventoryFlow.Application.Features.Transfers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transfers")]
public sealed class WarehouseTransfersController(ISender sender, ICurrentWorkspace currentWorkspace) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<WarehouseTransferResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseTransferResponse>> Record(RecordWarehouseTransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
    {
        var workspace = await currentWorkspace.GetAsync(cancellationToken);
        if (workspace is null) return Forbid();
        var transfer = await sender.Send(new RecordWarehouseTransferCommand(workspace.Id, request.SourceWarehouseId,
            request.DestinationWarehouseId, request.ProductId, request.Quantity, idempotencyKey ?? request.IdempotencyKey), cancellationToken);
        return transfer is null ? NotFound() : Created($"/api/transfers/{transfer.Id}", transfer);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<WarehouseTransferResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<WarehouseTransferResponse>>> List(CancellationToken cancellationToken)
    {
        var workspace = await currentWorkspace.GetAsync(cancellationToken);
        if (workspace is null) return Forbid();
        return Ok(await sender.Send(new ListWarehouseTransfersQuery(workspace.Id), cancellationToken));
    }
}
