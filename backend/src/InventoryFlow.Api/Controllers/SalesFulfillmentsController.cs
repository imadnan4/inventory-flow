using InventoryFlow.Application.Common.Tenancy;
using InventoryFlow.Application.Features.Sales;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sales/fulfillments")]
public sealed class SalesFulfillmentsController(ISender sender, ICurrentWorkspace currentWorkspace) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<SalesFulfillmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SalesFulfillmentResponse>> Record(RecordSalesFulfillmentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
    {
        var workspace = await currentWorkspace.GetAsync(cancellationToken);
        if (workspace is null) return Forbid();
        var fulfillment = await sender.Send(new RecordSalesFulfillmentCommand(workspace.Id, request.WarehouseId, request.ProductId,
            request.Quantity, idempotencyKey ?? request.IdempotencyKey), cancellationToken);
        return fulfillment is null ? NotFound() : Created($"/api/sales/fulfillments/{fulfillment.Id}", fulfillment);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SalesFulfillmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<SalesFulfillmentResponse>>> List(CancellationToken cancellationToken)
    {
        var workspace = await currentWorkspace.GetAsync(cancellationToken);
        if (workspace is null) return Forbid();
        return Ok(await sender.Send(new ListSalesFulfillmentsQuery(workspace.Id), cancellationToken));
    }
}
