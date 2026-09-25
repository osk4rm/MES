using AsistOff.MES.Production.Application.Features.AuditEvents;
using AsistOff.MES.Production.Application.Features.AuditEvents.Browse;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

/// <summary>
/// Append-only audit history (slice 2/2, issue #246). One row per create,
/// update and delete of Production Order, Machine and Production Confirmation,
/// readable per entity in descending time order. History rows are never
/// written through this controller — only browsed.
/// </summary>
[Route("api/audit-events")]
public class AuditEventsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AuditEventResponse>>> BrowseAsync(
        [FromQuery] BrowseAuditEventsRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);
}
