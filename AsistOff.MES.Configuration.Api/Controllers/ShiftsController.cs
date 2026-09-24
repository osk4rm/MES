using AsistOff.MES.Configuration.Application.Features.Shifts.Browse;
using AsistOff.MES.Configuration.Application.Features.Shifts.Create;
using AsistOff.MES.Configuration.Application.Features.Shifts.Delete;
using AsistOff.MES.Configuration.Application.Features.Shifts.Get;
using AsistOff.MES.Configuration.Application.Features.Shifts.Responses;
using AsistOff.MES.Configuration.Application.Features.Shifts.Update;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/shifts")]
public class ShiftsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ShiftResponse>>> BrowseAsync(
        [FromQuery] BrowseShiftsRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShiftResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetShiftRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ShiftResponse>> CreateAsync(
        [FromBody] CreateShiftRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id, [FromBody] UpdateShiftRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteShiftRequest(id), cancellationToken);
        return NoContent();
    }
}
