using AsistOff.MES.Production.Application.Features.SpcMeasurements;
using AsistOff.MES.Production.Application.Features.SpcMeasurements.Browse;
using AsistOff.MES.Production.Application.Features.SpcMeasurements.Chart;
using AsistOff.MES.Production.Application.Features.SpcMeasurements.Get;
using AsistOff.MES.Production.Application.Features.SpcMeasurements.Record;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

/// <summary>
/// Append-only SPC measurement log plus a read-only control chart
/// evaluation. There are intentionally no PUT/DELETE endpoints:
/// measurements are immutable once recorded.
/// </summary>
[Route("api/spc-measurements")]
public class SpcMeasurementsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<SpcMeasurementResponse>>> BrowseAsync(
        [FromQuery] BrowseSpcMeasurementsRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("chart")]
    public async Task<ActionResult<SpcMeasurementChartResponse>> ChartAsync(
        [FromQuery] GetSpcMeasurementChartRequest request, CancellationToken cancellationToken)
        => Ok(await sender.Send(request, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SpcMeasurementResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetSpcMeasurementRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<SpcMeasurementResponse>> RecordAsync(
        [FromBody] RecordSpcMeasurementRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }
}
