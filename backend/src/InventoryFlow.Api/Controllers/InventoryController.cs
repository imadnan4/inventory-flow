using InventoryFlow.Application.Common.Tenancy;
using InventoryFlow.Application.Features.Inventory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryFlow.Api.Controllers;

/// <summary>Provides workspace-scoped inventory receipt, issue, and balance endpoints.</summary>
[ApiController]
[Authorize]
[Route("api/inventory")]
public sealed class InventoryController(ISender sender, ICurrentWorkspace currentWorkspace) : ControllerBase
{
    /// <summary>Records a stock receipt exactly once for an idempotency key.</summary>
    [HttpPost("receipts")]
    [ProducesResponseType<InventoryMovementResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InventoryMovementResponse>> Receive(RecordInventoryMovementRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
    {
        var workspace = await currentWorkspace.GetAsync(cancellationToken);
        if (workspace is null) return Forbid();
        var movement = await sender.Send(new RecordReceiptCommand(workspace.Id, request.WarehouseId, request.ProductId,
            request.Quantity, idempotencyKey ?? request.IdempotencyKey), cancellationToken);
        return movement is null ? NotFound() : Created($"/api/inventory/movements/{movement.Id}", movement);
    }

    /// <summary>Records a stock issue exactly once for an idempotency key.</summary>
    [HttpPost("issues")]
    [ProducesResponseType<InventoryMovementResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryMovementResponse>> Issue(RecordInventoryMovementRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
    {
        var workspace = await currentWorkspace.GetAsync(cancellationToken);
        if (workspace is null) return Forbid();
        var movement = await sender.Send(new RecordIssueCommand(workspace.Id, request.WarehouseId, request.ProductId,
            request.Quantity, idempotencyKey ?? request.IdempotencyKey), cancellationToken);
        return movement is null ? NotFound() : Created($"/api/inventory/movements/{movement.Id}", movement);
    }

    /// <summary>Lists current on-hand balances for the current workspace.</summary>
    [HttpGet("balances")]
    [ProducesResponseType<IReadOnlyList<InventoryBalanceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<InventoryBalanceResponse>>> ListBalances(Guid? warehouseId, Guid? productId,
        CancellationToken cancellationToken)
    {
        var workspace = await currentWorkspace.GetAsync(cancellationToken);
        if (workspace is null) return Forbid();
        return Ok(await sender.Send(new ListInventoryBalancesQuery(workspace.Id, warehouseId, productId), cancellationToken));
    }
}
