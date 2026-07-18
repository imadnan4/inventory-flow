using InventoryFlow.Application.Common.Tenancy;
using InventoryFlow.Application.Features.Products;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryFlow.Api.Controllers;

/// <summary>Provides workspace-scoped product catalog endpoints.</summary>
[ApiController]
[Authorize]
[Route("api/products")]
public sealed class ProductsController(ISender sender, ICurrentWorkspace currentWorkspace) : ControllerBase
{
    /// <summary>Creates a product in the current workspace.</summary>
    [HttpPost]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var workspace = await currentWorkspace.GetAsync(cancellationToken);
        if (workspace is null) return Forbid();
        var product = await sender.Send(new CreateProductCommand(workspace.Id, request.Name, request.Sku), cancellationToken);
        return Created($"/api/products/{product.Id}", product);
    }

    /// <summary>Lists active products in the current workspace.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> List(CancellationToken cancellationToken)
    {
        var workspace = await currentWorkspace.GetAsync(cancellationToken);
        if (workspace is null) return Forbid();
        return Ok(await sender.Send(new ListProductsQuery(workspace.Id), cancellationToken));
    }

    /// <summary>Idempotently archives a product in the current workspace.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var workspace = await currentWorkspace.GetAsync(cancellationToken);
        if (workspace is null) return Forbid();
        return await sender.Send(new ArchiveProductCommand(workspace.Id, id), cancellationToken) ? NoContent() : NotFound();
    }
}
