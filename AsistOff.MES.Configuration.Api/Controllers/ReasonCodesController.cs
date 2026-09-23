using AsistOff.MES.Configuration.Application.Features.ReasonCodes.Browse;
using AsistOff.MES.Configuration.Application.Features.ReasonCodes.Create;
using AsistOff.MES.Configuration.Application.Features.ReasonCodes.Delete;
using AsistOff.MES.Configuration.Application.Features.ReasonCodes.Get;
using AsistOff.MES.Configuration.Application.Features.ReasonCodes.Responses;
using AsistOff.MES.Configuration.Application.Features.ReasonCodes.Update;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/reason-codes")]
public class ReasonCodesController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ReasonCodeResponse>>> BrowseAsync(
        [FromQuery] BrowseReasonCodesRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReasonCodeResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetReasonCodeRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ReasonCodeResponse>> CreateAsync(
        [FromBody] CreateReasonCodeRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id, [FromBody] UpdateReasonCodeRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteReasonCodeRequest(id), cancellationToken);
        return NoContent();
    }
}
