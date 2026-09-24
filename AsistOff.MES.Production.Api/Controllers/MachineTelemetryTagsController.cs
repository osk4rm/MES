using AsistOff.MES.Production.Application.Features.MachineTelemetryTags;
using AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Browse;
using AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Create;
using AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Delete;
using AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Get;
using AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Toggle;
using AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Update;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/telemetry-tags")]
public class MachineTelemetryTagsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<MachineTelemetryTagResponse>>> BrowseAsync(
        [FromQuery] BrowseMachineTelemetryTagsRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MachineTelemetryTagResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetMachineTelemetryTagRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<MachineTelemetryTagResponse>> CreateAsync(
        [FromBody] CreateMachineTelemetryTagRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id, [FromBody] UpdateMachineTelemetryTagRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/toggle")]
    public async Task<ActionResult<MachineTelemetryTagResponse>> ToggleAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new ToggleMachineTelemetryTagRequest(id), cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteMachineTelemetryTagRequest(id), cancellationToken);
        return NoContent();
    }
}
