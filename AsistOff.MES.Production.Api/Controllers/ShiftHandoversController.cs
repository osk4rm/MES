using AsistOff.MES.Production.Application.Features.ShiftHandovers;
using AsistOff.MES.Production.Application.Features.ShiftHandovers.Browse;
using AsistOff.MES.Production.Application.Features.ShiftHandovers.Create;
using AsistOff.MES.Production.Application.Features.ShiftHandovers.Get;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/shift-handovers")]
public class ShiftHandoversController(ISender sender) : ApiController
{
    /// <summary>
    /// Read-only shift handover context for the requested window (max 24h):
    /// resolved shift, open orders, active Andon signals and recent operator
    /// confirmations. Machine is optional; when omitted the context is
    /// tenant-wide with no single shift.
    /// </summary>
    [HttpGet("context")]
    public async Task<ActionResult<ShiftHandoverContextResponse>> GetContextAsync(
        [FromQuery] Guid? machineId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? confirmationPage,
        [FromQuery] int? confirmationPageSize,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new GetShiftHandoverContextRequest(machineId, from, to, confirmationPage, confirmationPageSize),
            cancellationToken));

    /// <summary>
    /// Persists one shift handover logbook entry. History is append-only:
    /// there are no update or delete endpoints, corrections are new entries.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ShiftHandoverResponse>> CreateAsync(
        [FromBody] CreateShiftHandoverRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    /// <summary>
    /// Paged handover logbook, newest boundary first, optionally filtered by
    /// machine and by boundary start inside [from, to].
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedShiftHandoversResponse>> BrowseAsync(
        [FromQuery] BrowseShiftHandoversRequest request, CancellationToken cancellationToken)
        => Ok(await sender.Send(request, cancellationToken));

    /// <summary>
    /// One handover entry with its notes, machine, shift window, author and
    /// creation-time context snapshot counts.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShiftHandoverResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetShiftHandoverByIdRequest(id), cancellationToken));
}
