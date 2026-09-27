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

    /// <summary>
    /// Operator shift queue: the roster assignment covering now for
    /// <c>operatorCode</c>, Released/InProgress orders overlapping that shift
    /// window ordered next-up (priority, then due date), and the open Andon
    /// signals for the queued Work Centers. <c>take</c> caps the queue and is
    /// clamped to 200 rows. An operator with no covering assignment gets an
    /// empty queue with operator context; an unknown code yields 404.
    /// </summary>
    [HttpGet("operator-queue")]
    public async Task<ActionResult<OperatorShiftQueueResponse>> GetOperatorQueueAsync(
        [FromQuery] string? operatorCode, [FromQuery] int? take, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetOperatorShiftQueueRequest(operatorCode ?? string.Empty, take), cancellationToken));

    /// <summary>Time-phased Gantt schedule: computed operation segments grouped per Work Center, with manual overrides overlaid.</summary>
    [HttpGet("gantt")]
    public async Task<ActionResult<GanttScheduleResponse>> GetGanttAsync(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? machineId, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetGanttScheduleRequest(from, to, machineId), cancellationToken));

    /// <summary>
    /// Move a Gantt segment: pins the operation ({id} is the OperationNodeId)
    /// of the given Production Order to a new window and Work Center. The body
    /// must carry the order <c>concurrencyToken</c> from the last <c>GET</c>; a
    /// stale token is rejected with 409 carrying the current token (reload the
    /// order, re-apply the move, resubmit). An overlapping move on the same
    /// Work Center is rejected with 409 listing the conflicting segment ids
    /// unless <c>force</c> is set.
    /// </summary>
    [HttpPut("gantt/segments/{id:guid}")]
    public async Task<ActionResult<RescheduleGanttSegmentResponse>> RescheduleSegmentAsync(
        [FromRoute] Guid id, [FromBody] RescheduleGanttSegmentBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new RescheduleGanttSegmentRequest(
                id,
                body.ProductionOrderId,
                body.PlannedStart,
                body.PlannedEnd,
                body.MachineId,
                body.ConcurrencyToken,
                body.Force,
                body.Notes),
            cancellationToken));

    public sealed record RescheduleGanttSegmentBody(
        Guid ProductionOrderId,
        DateTime PlannedStart,
        DateTime PlannedEnd,
        Guid MachineId,
        string? ConcurrencyToken,
        bool Force,
        string? Notes);
}
