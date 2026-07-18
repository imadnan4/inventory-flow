using InventoryFlow.Application.Common.Tenancy;
using InventoryFlow.Application.Features.Warehouses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace InventoryFlow.Api.Controllers; [ApiController][Authorize][Route("api/warehouses")] public sealed class WarehousesController(ISender s, ICurrentWorkspace current) : ControllerBase { [HttpPost] public async Task<ActionResult<WarehouseResponse>> Create(CreateWarehouseRequest r, CancellationToken c) { var w = await current.GetAsync(c); if (w is null) return Forbid(); var x = await s.Send(new CreateWarehouseCommand(w.Id, r.Name), c); return Created($"/api/warehouses/{x.Id}", x); } [HttpGet] public async Task<ActionResult<IReadOnlyList<WarehouseResponse>>> List(CancellationToken c) { var w = await current.GetAsync(c); return w is null ? Forbid() : Ok(await s.Send(new ListWarehousesQuery(w.Id), c)); } [HttpDelete("{id:guid}")] public async Task<IActionResult> Archive(Guid id, CancellationToken c) { var w = await current.GetAsync(c); if (w is null) return Forbid(); return await s.Send(new ArchiveWarehouseCommand(w.Id, id), c) ? NoContent() : NotFound(); } }
