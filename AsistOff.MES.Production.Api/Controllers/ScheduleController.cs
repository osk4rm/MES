using AsistOff.MES.Production.Application.Features.Schedule;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/schedule")]
public class ScheduleController(ISender sender) : ApiController
{
    /// <summary>Shift-aware dispatch board: day buckets with active shifts, roster headcounts and ordered Released/InProgress order rows.</summary>
    [HttpGet("dispatch")]
    public async Task<ActionResult<DispatchBoardResponse>> GetDispatchAsync(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetDispatchBoardRequest(from, to), cancellationToken));

    /// <summary>Time-phased Gantt schedule: computed operation segments grouped per Work Center, with manual overrides overlaid.</summary>
    [HttpGet("gantt")]
    public async Task<ActionResult<GanttScheduleResponse>> GetGanttAsync(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? machineId, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetGanttScheduleRequest(from, to, machineId), cancellationToken));
}
