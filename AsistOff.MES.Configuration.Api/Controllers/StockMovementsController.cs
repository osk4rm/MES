using AsistOff.MES.Configuration.Application.Features.StockMovements.Browse;
using AsistOff.MES.Configuration.Application.Features.StockMovements.Responses;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/stock-movements")]
public class StockMovementsController(ISender mediator) : ApiController
{
    /// <summary>Stock ledger lines posted for one confirmation.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StockMovementResponse>>> Browse(
        [FromQuery] BrowseStockMovementsRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(request, cancellationToken);
        return Ok(result);
    }
}
