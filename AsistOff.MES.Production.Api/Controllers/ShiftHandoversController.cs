using AsistOff.MES.Production.Application.Features.ShiftHandovers;
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
}
