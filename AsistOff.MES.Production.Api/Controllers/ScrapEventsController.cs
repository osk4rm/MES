using AsistOff.MES.Production.Application.Features.ScrapEvents;
using AsistOff.MES.Production.Application.Features.ScrapEvents.Browse;
using AsistOff.MES.Production.Application.Features.ScrapEvents.Create;
using AsistOff.MES.Production.Application.Features.ScrapEvents.Delete;
using AsistOff.MES.Production.Application.Features.ScrapEvents.Get;
using AsistOff.MES.Production.Application.Features.ScrapEvents.Update;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/scrap-events")]
public class ScrapEventsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ScrapEventResponse>>> BrowseAsync(
        [FromQuery] BrowseScrapEventsRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ScrapEventResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetScrapEventRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ScrapEventResponse>> CreateAsync(
        [FromBody] CreateScrapEventRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id, [FromBody] UpdateScrapEventRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteScrapEventRequest(id), cancellationToken);
        return NoContent();
    }
}
