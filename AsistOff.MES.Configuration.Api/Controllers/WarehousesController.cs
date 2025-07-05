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
    public async Task<IActionResult> Browse(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new BrowseWarehousesRequest(), cancellationToken);

        return result.Match(
            onValue: x => Ok(new WarehousesResponse(x.ToResponse())),
            onError: Problem);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetWarehouseRequest(id), cancellationToken);
        
        return result.Match(
            onValue: x => Ok(x.ToResponse()),
            onError: Problem);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        
        return result.Match(
            onValue: x => CreatedAtAction(nameof(Get), new { id = x.Id }, x.ToResponse()),
            onError: Problem);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(UpdateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        
        return result.Match(
            onValue: x => NoContent(),
            onError: Problem);
    }
}