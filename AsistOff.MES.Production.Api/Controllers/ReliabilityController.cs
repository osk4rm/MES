using AsistOff.MES.Production.Application.Features.Reliability;
using AsistOff.MES.Production.Application.Features.Reliability.Trend;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/reliability")]
public class ReliabilityController(ISender sender) : ApiController
{
    /// <summary>Read-only per-Work Center MTBF/MTTR snapshot over a UTC time window.</summary>
    [HttpGet("snapshot")]
    public async Task<ActionResult<ReliabilitySnapshotResponse>> SnapshotAsync(
        [FromQuery] Guid machineId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new GetReliabilitySnapshotRequest(machineId, fromUtc, toUtc),
            cancellationToken));

    /// <summary>Per-Work Center MTBF/MTTR trend: one snapshot per Day or Week bucket.</summary>
    [HttpGet("trend")]
    public async Task<ActionResult<ReliabilityTrendResponse>> TrendAsync(
        [FromQuery] Guid machineId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] string? bucket,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new GetReliabilityTrendRequest(machineId, fromUtc, toUtc, bucket),
            cancellationToken));
}
