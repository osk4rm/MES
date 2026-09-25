using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Browse;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Create;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Delete;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Get;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Movements;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/production-confirmations")]
public class ProductionConfirmationsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ProductionConfirmationResponse>>> BrowseAsync(
        [FromQuery] BrowseProductionConfirmationsRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductionConfirmationResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetProductionConfirmationRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ProductionConfirmationResponse>> CreateAsync(
        [FromBody] CreateProductionConfirmationRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteProductionConfirmationRequest(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Read-only RW/PW movement preview for a single confirmation.</summary>
    [HttpGet("{id:guid}/movements")]
    public async Task<ActionResult<IReadOnlyList<MovementPreviewLine>>> GetMovementsAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new BrowseConfirmationMovementsRequest(id), cancellationToken));
}
