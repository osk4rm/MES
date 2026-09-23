using AsistOff.MES.Configuration.Application.Features.Machines.Browse;
using AsistOff.MES.Configuration.Application.Features.Machines.Create;
using AsistOff.MES.Configuration.Application.Features.Machines.Delete;
using AsistOff.MES.Configuration.Application.Features.Machines.Get;
using AsistOff.MES.Configuration.Application.Features.Machines.Responses;
using AsistOff.MES.Configuration.Application.Features.Machines.Update;
using AsistOff.MES.Configuration.Application.Features.WorkCenterCalendar.Get;
using AsistOff.MES.Configuration.Application.Features.WorkCenterCalendar.Responses;
using AsistOff.MES.Configuration.Application.Features.WorkCenterCalendar.Save;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/machines")]
public class MachinesController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<MachineResponse>>> BrowseAsync(
        [FromQuery] BrowseMachinesRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MachineResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetMachineRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<MachineResponse>> CreateAsync(
        [FromBody] CreateMachineRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id, [FromBody] UpdateMachineRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteMachineRequest(id), cancellationToken);
        return NoContent();
    }

    [HttpGet("{machineId:guid}/calendar")]
    public async Task<ActionResult<WorkCenterCalendarResponse>> GetCalendarAsync(
        [FromRoute] Guid machineId, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetWorkCenterCalendarRequest(machineId), cancellationToken));

    [HttpPut("{machineId:guid}/calendar")]
    public async Task<ActionResult<WorkCenterCalendarResponse>> SaveCalendarAsync(
        [FromRoute] Guid machineId, [FromBody] SaveWorkCenterCalendarRequest body, CancellationToken cancellationToken)
        => Ok(await sender.Send(body with { MachineId = machineId }, cancellationToken));
}
