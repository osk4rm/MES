using AsistOff.MES.Configuration.Application.Features.StockMovements.Responses;
using AsistOff.MES.Configuration.Application.Features.StockMovements.StockOnHand;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/stock-on-hand")]
public class StockOnHandController(ISender mediator) : ApiController
{
    /// <summary>Signed stock balances (PW receipts add, RW issues subtract) grouped by product and warehouse.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StockOnHandResponse>>> Get(
        [FromQuery] GetStockOnHandRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(request, cancellationToken);
        return Ok(result);
    }
}
