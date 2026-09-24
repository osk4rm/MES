using AsistOff.MES.Production.Application.Features.Reliability;
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
}
