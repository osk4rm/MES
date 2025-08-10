using AsistOff.MES.Configuration.Api.Contracts.Warehouses;
using AsistOff.MES.Configuration.Application.Features.Warehouses.Browse;
using AsistOff.MES.Configuration.Application.Features.Warehouses.Create;
using AsistOff.MES.Configuration.Application.Features.Warehouses.Get;
using AsistOff.MES.Configuration.Application.Features.Warehouses.Update;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/warehouses")]
public class WarehousesController : ApiController
{
    private readonly ISender _mediator;

    public WarehousesController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<WarehousesResponse>> Browse(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new BrowseWarehousesRequest(), cancellationToken);
        var response = new WarehousesResponse(result.Select(x => x.ToResponse()).ToList());
        return response;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WarehouseResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetWarehouseRequest(id), cancellationToken);
        
        return new WarehouseResponse(result.Id, result.Name, result.SyncId);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        
        return CreatedAtAction(nameof(Get), new { id = result.Id },
            result.ToResponse());
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(UpdateWarehouseRequest request, CancellationToken cancellationToken)
    {
        await _mediator.Send(request, cancellationToken);
        return NoContent();
    }
}