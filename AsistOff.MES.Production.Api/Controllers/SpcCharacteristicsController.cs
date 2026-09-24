using AsistOff.MES.Production.Application.Features.SpcCharacteristics.Browse;
using AsistOff.MES.Production.Application.Features.SpcCharacteristics.Create;
using AsistOff.MES.Production.Application.Features.SpcCharacteristics.Delete;
using AsistOff.MES.Production.Application.Features.SpcCharacteristics.Get;
using AsistOff.MES.Production.Application.Features.SpcCharacteristics.Responses;
using AsistOff.MES.Production.Application.Features.SpcCharacteristics.Update;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/spc-characteristics")]
public class SpcCharacteristicsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<SpcCharacteristicResponse>>> BrowseAsync(
        [FromQuery] BrowseSpcCharacteristicsRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SpcCharacteristicResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetSpcCharacteristicRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<SpcCharacteristicResponse>> CreateAsync(
        [FromBody] CreateSpcCharacteristicRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id, [FromBody] UpdateSpcCharacteristicRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteSpcCharacteristicRequest(id), cancellationToken);
        return NoContent();
    }
}
