using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Browse;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Close;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Complete;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Create;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Delete;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Get;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Movements;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Release;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Update;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/production-orders")]
public class ProductionOrdersController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ProductionOrderResponse>>> BrowseAsync(
        [FromQuery] BrowseProductionOrdersRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductionOrderResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetProductionOrderRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ProductionOrderResponse>> CreateAsync(
        [FromBody] CreateProductionOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductionOrderResponse>> UpdateAsync(
        [FromRoute] Guid id, [FromBody] UpdateProductionOrderRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route ID does not match request ID");
        return Ok(await sender.Send(request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteProductionOrderRequest(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/release")]
    public async Task<ActionResult<ProductionOrderResponse>> ReleaseAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new ReleaseProductionOrderRequest(id), cancellationToken));

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<ProductionOrderResponse>> CompleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new CompleteProductionOrderRequest(id), cancellationToken));

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<ProductionOrderResponse>> CloseAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new CloseProductionOrderRequest(id), cancellationToken));

    /// <summary>Read-only RW/PW movement preview aggregated per order.</summary>
    [HttpGet("{id:guid}/movements")]
    public async Task<ActionResult<IReadOnlyList<MovementPreviewLine>>> GetMovementsAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new BrowseOrderMovementsRequest(id), cancellationToken));
}
