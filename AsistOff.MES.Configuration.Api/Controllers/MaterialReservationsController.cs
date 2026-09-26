using AsistOff.MES.Configuration.Application.Features.MaterialReservations;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/material-reservations")]
public class MaterialReservationsController(ISender mediator) : ApiController
{
    /// <summary>Paged soft material reservations with optional order, product, warehouse and status filters.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<MaterialReservationResponse>>> Browse(
        [FromQuery] BrowseMaterialReservationsRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Single reservation by id; rows of another tenant surface as 404.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaterialReservationResponse>> Get(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetMaterialReservationRequest(id), cancellationToken);
        return Ok(result);
    }
}
