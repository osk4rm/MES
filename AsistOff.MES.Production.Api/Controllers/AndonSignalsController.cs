using AsistOff.MES.Production.Application.Features.AndonSignals;
using AsistOff.MES.Production.Application.Features.AndonSignals.Acknowledge;
using AsistOff.MES.Production.Application.Features.AndonSignals.Browse;
using AsistOff.MES.Production.Application.Features.AndonSignals.Delete;
using AsistOff.MES.Production.Application.Features.AndonSignals.Get;
using AsistOff.MES.Production.Application.Features.AndonSignals.Raise;
using AsistOff.MES.Production.Application.Features.AndonSignals.Resolve;
using AsistOff.MES.Production.Application.Features.AndonSignals.Update;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/andon-signals")]
public class AndonSignalsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AndonSignalResponse>>> BrowseAsync(
        [FromQuery] BrowseAndonSignalsRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AndonSignalResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetAndonSignalRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<AndonSignalResponse>> RaiseAsync(
        [FromBody] RaiseAndonSignalRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/acknowledge")]
    public async Task<ActionResult<AndonSignalResponse>> AcknowledgeAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new AcknowledgeAndonSignalRequest(id), cancellationToken));

    [HttpPost("{id:guid}/resolve")]
    public async Task<ActionResult<AndonSignalResponse>> ResolveAsync(
        [FromRoute] Guid id, [FromBody] ResolveAndonSignalBody? body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new ResolveAndonSignalRequest(id, body?.ResolvedAt), cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AndonSignalResponse>> UpdateAsync(
        [FromRoute] Guid id, [FromBody] UpdateAndonSignalRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route ID does not match request ID");
        return Ok(await sender.Send(request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteAndonSignalRequest(id), cancellationToken);
        return NoContent();
    }
}

/// <summary>Optional resolve payload; when omitted the signal is resolved as of now.</summary>
public sealed record ResolveAndonSignalBody(DateTime? ResolvedAt);
