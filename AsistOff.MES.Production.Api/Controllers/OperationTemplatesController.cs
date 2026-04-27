using AsistOff.MES.Production.Application.Features.OperationTemplates;
using AsistOff.MES.Production.Application.Features.OperationTemplates.Browse;
using AsistOff.MES.Production.Application.Features.OperationTemplates.Create;
using AsistOff.MES.Production.Application.Features.OperationTemplates.Delete;
using AsistOff.MES.Production.Application.Features.OperationTemplates.Get;
using AsistOff.MES.Production.Application.Features.OperationTemplates.Update;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/operation-templates")]
public class OperationTemplatesController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<OperationTemplateResponse>>> BrowseAsync(
        [FromQuery] BrowseOperationTemplatesRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OperationTemplateResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetOperationTemplateRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<OperationTemplateResponse>> CreateAsync(
        [FromBody] CreateOperationTemplateRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id, [FromBody] UpdateOperationTemplateRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteOperationTemplateRequest(id), cancellationToken);
        return NoContent();
    }
}
