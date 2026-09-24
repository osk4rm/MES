using AsistOff.MES.Production.Application.Features.Lots;
using AsistOff.MES.Production.Application.Features.Lots.Browse;
using AsistOff.MES.Production.Application.Features.Lots.ChangeStatus;
using AsistOff.MES.Production.Application.Features.Lots.Create;
using AsistOff.MES.Production.Application.Features.Lots.Delete;
using AsistOff.MES.Production.Application.Features.Lots.Get;
using AsistOff.MES.Production.Application.Features.Lots.GetByCode;
using AsistOff.MES.Production.Application.Features.Lots.Update;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/lots")]
public class LotsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<LotResponse>>> BrowseAsync(
        [FromQuery] BrowseLotsRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("by-code/{code}")]
    public async Task<ActionResult<LotResponse>> GetByCodeAsync(
        [FromRoute] string code, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetLotByCodeRequest(code), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LotResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetLotRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<LotResponse>> CreateAsync(
        [FromBody] CreateLotRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id, [FromBody] UpdateLotRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult> ChangeStatusAsync(
        [FromRoute] Guid id, [FromBody] ChangeLotStatusBody body, CancellationToken cancellationToken)
    {
        await sender.Send(new ChangeLotStatusRequest(id, body.Status), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteLotRequest(id), cancellationToken);
        return NoContent();
    }

    public sealed record ChangeLotStatusBody(LotStatus Status);
}
