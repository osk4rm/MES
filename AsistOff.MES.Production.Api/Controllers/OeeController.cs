using AsistOff.MES.Production.Application.Features.Oee.Losses;
using AsistOff.MES.Production.Application.Features.Oee.Snapshot;
using AsistOff.MES.Production.Application.Features.Oee.Summary;
using AsistOff.MES.Production.Application.Features.Oee.Trend;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/oee")]
public class OeeController(ISender sender) : ApiController
{
    /// <summary>Per-Work Center OEE summary: confirmation counts, the Quality factor and the Availability factor over a UTC time window.</summary>
    [HttpGet]
    public async Task<ActionResult<OeeSummaryResponse>> SummaryAsync(
        [FromQuery] Guid machineId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new GetOeeSummaryRequest(machineId, fromUtc, toUtc),
            cancellationToken));

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

    /// <summary>Per-Work Center OEE trend: one (1/3) snapshot per Day or Week bucket.</summary>
    [HttpGet("trend")]
    public async Task<ActionResult<OeeTrendResponse>> TrendAsync(
        [FromQuery] Guid machineId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] decimal idealCycleTimeSeconds,
        [FromQuery] string? bucket,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new GetOeeTrendRequest(machineId, fromUtc, toUtc, idealCycleTimeSeconds, bucket),
            cancellationToken));

    /// <summary>Per-Work Center loss Pareto: downtime minutes and scrap quantities by reason code.</summary>
    [HttpGet("losses")]
    public async Task<ActionResult<OeeLossesResponse>> LossesAsync(
        [FromQuery] Guid machineId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new GetOeeLossesRequest(machineId, fromUtc, toUtc),
            cancellationToken));
}
