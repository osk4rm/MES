using AsistOff.MES.Configuration.Application.Features.ProductGroups.Browse;
using AsistOff.MES.Configuration.Application.Features.ProductGroups.Create;
using AsistOff.MES.Configuration.Application.Features.ProductGroups.Delete;
using AsistOff.MES.Configuration.Application.Features.ProductGroups.Get;
using AsistOff.MES.Configuration.Application.Features.ProductGroups.Update;
using AsistOff.MES.Configuration.Application.Features.ProductGroups.Common.Responses;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/product-groups")]
public class ProductGroupsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ProductGroupResponse>>> BrowseAsync(
        [FromQuery] BrowseProductGroupsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return result;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductGroupResponse>> GetAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var request = new GetProductGroupRequest(id);
        var result = await sender.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ProductGroupResponse>> CreateAsync(
        [FromBody] CreateProductGroupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction(nameof(GetAsync), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateProductGroupRequest request,
        CancellationToken cancellationToken)
    {
        if (id != request.Id)
        {
            return BadRequest("Route ID does not match request ID");
        }

        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var request = new DeleteProductGroupRequest(id);
        await sender.Send(request, cancellationToken);
        return NoContent();
    }
}
