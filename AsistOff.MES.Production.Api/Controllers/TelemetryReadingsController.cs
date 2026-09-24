using AsistOff.MES.Production.Application.Features.TelemetryReadings;
using AsistOff.MES.Production.Application.Features.TelemetryReadings.Browse;
using AsistOff.MES.Production.Application.Features.TelemetryReadings.Get;
using AsistOff.MES.Production.Application.Features.TelemetryReadings.Submit;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
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
    [HttpGet]
    public async Task<ActionResult<PagedResponse<TelemetryReadingResponse>>> BrowseAsync(
        [FromQuery] BrowseTelemetryReadingsRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

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
