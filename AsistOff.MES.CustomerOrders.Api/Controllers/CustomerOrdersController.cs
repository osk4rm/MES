using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Browse;
using AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Create;
using AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Delete;
using AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Get;
using AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Lines;
using AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.ReleaseToProduction;
using AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Update;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.CustomerOrders.Api.Controllers;

[Route("api/customer-orders")]
public class CustomerOrdersController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<CustomerOrderResponse>>> BrowseAsync([FromQuery] BrowseCustomerOrdersRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerOrderResponse>> GetAsync([FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetCustomerOrderRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<CustomerOrderResponse>> CreateAsync([FromBody] CreateCustomerOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync([FromRoute] Guid id, [FromBody] UpdateCustomerOrderRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteCustomerOrderRequest(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{orderId:guid}/lines")]
    public async Task<ActionResult<CustomerOrderResponse>> AddLineAsync([FromRoute] Guid orderId, [FromBody] AddCustomerOrderLineRequest request, CancellationToken cancellationToken)
    {
        if (orderId != request.CustomerOrderId) return BadRequest("Route ID does not match request ID");
        return Ok(await sender.Send(request, cancellationToken));
    }

    [HttpPut("lines/{id:guid}")]
    public async Task<ActionResult<CustomerOrderResponse>> UpdateLineAsync([FromRoute] Guid id, [FromBody] UpdateCustomerOrderLineRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route ID does not match request ID");
        return Ok(await sender.Send(request, cancellationToken));
    }

    [HttpDelete("lines/{id:guid}")]
    public async Task<ActionResult<CustomerOrderResponse>> DeleteLineAsync([FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new DeleteCustomerOrderLineRequest(id), cancellationToken));

    [HttpPost("lines/{id:guid}/production-releases")]
    public async Task<ActionResult<CustomerOrderResponse>> ReleaseLineToProductionAsync([FromRoute] Guid id, [FromBody] CreateProductionReleaseRequest request, CancellationToken cancellationToken)
    {
        if (id != request.CustomerOrderLineId) return BadRequest("Route ID does not match request ID");
        return Ok(await sender.Send(request, cancellationToken));
    }
}
