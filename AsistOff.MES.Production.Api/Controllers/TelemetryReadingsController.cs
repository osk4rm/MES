using System.Text;
using AsistOff.MES.Production.Application.Features.TelemetryReadings;
using AsistOff.MES.Production.Application.Features.TelemetryReadings.Browse;
using AsistOff.MES.Production.Application.Features.TelemetryReadings.Export;
using AsistOff.MES.Production.Application.Features.TelemetryReadings.Get;
using AsistOff.MES.Production.Application.Features.TelemetryReadings.Submit;
using AsistOff.MES.Production.Application.Features.TelemetryReadings.Trend;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

/// <summary>
/// Append-only reading log. There are intentionally no PUT/DELETE endpoints:
/// machine readings are immutable once recorded.
/// </summary>
[Route("api/telemetry-readings")]
public class TelemetryReadingsController(ISender sender) : ApiController
{
    /// <summary>
    /// Browses the reading log as JSON, or as CSV (at most 5000 data rows,
    /// same filters) when the caller sends <c>Accept: text/csv</c>.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> BrowseAsync(
        [FromQuery] BrowseTelemetryReadingsRequest request, CancellationToken cancellationToken)
    {
        if (Request.Headers.Accept.Any(h => h is not null && h.Contains("text/csv", StringComparison.OrdinalIgnoreCase)))
        {
            var csv = await sender.Send(new ExportTelemetryReadingsRequest
            {
                TagId = request.TagId,
                MachineId = request.MachineId,
                ReadAtFrom = request.ReadAtFrom,
                ReadAtTo = request.ReadAtTo,
                LatestOnly = request.LatestOnly
            }, cancellationToken);

            return File(Encoding.UTF8.GetBytes(csv), "text/csv", "telemetry-readings.csv");
        }

        return Ok(await sender.Send(request, cancellationToken));
    }

    /// <summary>
    /// Last-N readings of one tag in ascending time order (oldest first)
    /// for dashboard sparklines. <paramref name="request"/> carries the tag
    /// id and the capped take (1..200).
    /// </summary>
    [HttpGet("trend")]
    public async Task<ActionResult<IReadOnlyList<TelemetryReadingResponse>>> TrendAsync(
        [FromQuery] BrowseTelemetryTrendRequest request, CancellationToken cancellationToken)
        => Ok(await sender.Send(request, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TelemetryReadingResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTelemetryReadingRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<TelemetryReadingResponse>> SubmitAsync(
        [FromBody] SubmitTelemetryReadingRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }
}
