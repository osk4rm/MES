using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.CustomerOrders.Application.Features.Customers.Browse;
using AsistOff.MES.CustomerOrders.Application.Features.Customers.Create;
using AsistOff.MES.CustomerOrders.Application.Features.Customers.Delete;
using AsistOff.MES.CustomerOrders.Application.Features.Customers.Get;
using AsistOff.MES.CustomerOrders.Application.Features.Customers.Update;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.CustomerOrders.Api.Controllers;

[Route("api/customers")]
public class CustomersController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<CustomerResponse>>> BrowseAsync([FromQuery] BrowseCustomersRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> GetAsync([FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetCustomerRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> CreateAsync([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync([FromRoute] Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteCustomerRequest(id), cancellationToken);
        return NoContent();
    }
}
