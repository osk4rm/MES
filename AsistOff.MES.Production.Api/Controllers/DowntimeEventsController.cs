using AsistOff.MES.Production.Application.Features.DowntimeEvents;
using AsistOff.MES.Production.Application.Features.DowntimeEvents.Browse;
using AsistOff.MES.Production.Application.Features.DowntimeEvents.Close;
using AsistOff.MES.Production.Application.Features.DowntimeEvents.Delete;
using AsistOff.MES.Production.Application.Features.DowntimeEvents.Get;
using AsistOff.MES.Production.Application.Features.DowntimeEvents.Start;
using AsistOff.MES.Production.Application.Features.DowntimeEvents.Update;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/downtime-events")]
public class DowntimeEventsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<DowntimeEventResponse>>> BrowseAsync(
        [FromQuery] BrowseDowntimeEventsRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DowntimeEventResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetDowntimeEventRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<DowntimeEventResponse>> StartAsync(
        [FromBody] StartDowntimeEventRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<DowntimeEventResponse>> CloseAsync(
        [FromRoute] Guid id, [FromBody] CloseDowntimeEventBody? body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new CloseDowntimeEventRequest(id, body?.EndedAt), cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id, [FromBody] UpdateDowntimeEventRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteDowntimeEventRequest(id), cancellationToken);
        return NoContent();
    }

    public record CloseDowntimeEventBody(DateTime? EndedAt);
}
