using AsistOff.MES.Configuration.Application.Features.Warehouses.Browse;
using AsistOff.MES.Configuration.Application.Features.Warehouses.Create;
using AsistOff.MES.Configuration.Application.Features.Warehouses.Get;
using AsistOff.MES.Configuration.Application.Features.Warehouses.Update;
using AsistOff.MES.Configuration.Application.Features.Warehouses.Common.Responses;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/warehouses")]
public class WarehousesController(ISender mediator) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<WarehouseItemResponse>>> Browse([FromQuery] BrowseWarehousesRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(request, cancellationToken);
        return result;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WarehouseItemResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetWarehouseRequest(id), cancellationToken);
        
        return new WarehouseItemResponse(result.Id, result.Name, result.SyncId);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(request, cancellationToken);
        
        return CreatedAtAction(nameof(Get), new { id = result.Id },
            new WarehouseItemResponse(result.Id, result.Name, result.SyncId));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWarehouseRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest("Route ID does not match body ID.");

        await mediator.Send(request, cancellationToken);
        return NoContent();
    }
}