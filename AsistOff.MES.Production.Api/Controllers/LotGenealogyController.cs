using AsistOff.MES.Production.Application.Features.LotGenealogy;
using AsistOff.MES.Production.Application.Features.LotGenealogy.Browse;
using AsistOff.MES.Production.Application.Features.LotGenealogy.Delete;
using AsistOff.MES.Production.Application.Features.LotGenealogy.Get;
using AsistOff.MES.Production.Application.Features.LotGenealogy.Record;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/lot-genealogy")]
public class LotGenealogyController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<LotGenealogyEdgeResponse>>> BrowseAsync(
        [FromQuery] BrowseLotGenealogyEdgesRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LotGenealogyEdgeResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetLotGenealogyEdgeRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<LotGenealogyEdgeResponse>> RecordAsync(
        [FromBody] RecordLotGenealogyEdgeRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteLotGenealogyEdgeRequest(id), cancellationToken);
        return NoContent();
    }
}
