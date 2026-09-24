using AsistOff.MES.Production.Application.Features.Oee.Snapshot;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/oee")]
public class OeeController(ISender sender) : ApiController
{
    /// <summary>Read-only per-Work Center OEE snapshot over a UTC time window.</summary>
    [HttpGet("snapshot")]
    public async Task<ActionResult<OeeSnapshotResponse>> SnapshotAsync(
        [FromQuery] Guid machineId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] decimal idealCycleTimeSeconds,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new GetOeeSnapshotRequest(machineId, fromUtc, toUtc, idealCycleTimeSeconds),
            cancellationToken));
}
