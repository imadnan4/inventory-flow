using InventoryFlow.Application.Common.Tenancy;
using InventoryFlow.Application.Features.Purchases;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/purchases/receipts")]
public sealed class PurchaseReceiptsController(ISender sender, ICurrentWorkspace currentWorkspace) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<PurchaseReceiptResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseReceiptResponse>> Record(RecordPurchaseReceiptRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
    {
        var workspace = await currentWorkspace.GetAsync(cancellationToken);
        if (workspace is null) return Forbid();
        var receipt = await sender.Send(new RecordPurchaseReceiptCommand(workspace.Id, request.SupplierId, request.WarehouseId,
            request.ProductId, request.Quantity, idempotencyKey ?? request.IdempotencyKey), cancellationToken);
        return receipt is null ? NotFound() : Created($"/api/purchases/receipts/{receipt.Id}", receipt);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PurchaseReceiptResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<PurchaseReceiptResponse>>> List(CancellationToken cancellationToken)
    {
        var workspace = await currentWorkspace.GetAsync(cancellationToken);
        if (workspace is null) return Forbid();
        return Ok(await sender.Send(new ListPurchaseReceiptsQuery(workspace.Id), cancellationToken));
    }
}
