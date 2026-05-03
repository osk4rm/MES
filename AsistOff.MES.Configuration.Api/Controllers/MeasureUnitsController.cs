using AsistOff.MES.Configuration.Application.Features.MeasureUnits.Browse;
using AsistOff.MES.Configuration.Application.Features.MeasureUnits.Create;
using AsistOff.MES.Configuration.Application.Features.MeasureUnits.Delete;
using AsistOff.MES.Configuration.Application.Features.MeasureUnits.Get;
using AsistOff.MES.Configuration.Application.Features.MeasureUnits.Responses;
using AsistOff.MES.Configuration.Application.Features.MeasureUnits.Update;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/measure-units")]
public class MeasureUnitsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<MeasureUnitResponse>>> BrowseAsync(
        [FromQuery] BrowseMeasureUnitsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return result;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MeasureUnitResponse>> GetAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var request = new GetMeasureUnitRequest(id);
        var result = await sender.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<MeasureUnitResponse>> CreateAsync(
        [FromBody] CreateMeasureUnitRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateMeasureUnitRequest request,
        CancellationToken cancellationToken)
    {
        if (id != request.Id)
        {
            return BadRequest("Route ID does not match request ID");
        }

        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var request = new DeleteMeasureUnitRequest(id);
        await sender.Send(request, cancellationToken);
        return NoContent();
    }
}
