using AsistOff.MES.Configuration.Application.Features.Products.Browse;
using AsistOff.MES.Configuration.Application.Features.Products.Create;
using AsistOff.MES.Configuration.Application.Features.Products.Delete;
using AsistOff.MES.Configuration.Application.Features.Products.Get;
using AsistOff.MES.Configuration.Application.Features.Products.Update;
using AsistOff.MES.Configuration.Application.Features.Products.Common.Responses;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/products")]
public class ProductsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ProductResponse>>> BrowseAsync(
        [FromQuery] BrowseProductsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return result;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var request = new GetProductRequest(id);
        var result = await sender.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> CreateAsync(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction(nameof(GetAsync), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateProductRequest request,
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
        var request = new DeleteProductRequest(id);
        await sender.Send(request, cancellationToken);
        return NoContent();
    }
}