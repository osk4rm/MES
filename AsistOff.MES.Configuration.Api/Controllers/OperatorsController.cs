using AsistOff.MES.Configuration.Application.Features.Operators.Browse;
using AsistOff.MES.Configuration.Application.Features.Operators.Create;
using AsistOff.MES.Configuration.Application.Features.Operators.Delete;
using AsistOff.MES.Configuration.Application.Features.Operators.Get;
using AsistOff.MES.Configuration.Application.Features.Operators.Responses;
using AsistOff.MES.Configuration.Application.Features.Operators.Update;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/operators")]
public class OperatorsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<OperatorResponse>>> BrowseAsync(
        [FromQuery] BrowseOperatorsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);

        return result;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OperatorResponse>> GetAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var request = new GetOperatorRequest(id);
        var result = await sender.Send(request, cancellationToken);

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<OperatorResponse>> CreateAsync(
        [FromBody] CreateOperatorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);

        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateOperatorRequest request,
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
        var request = new DeleteOperatorRequest(id);
        await sender.Send(request, cancellationToken);

        return NoContent();
    }
}