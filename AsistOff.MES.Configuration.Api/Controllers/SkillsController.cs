using AsistOff.MES.Configuration.Application.Features.Skills.Browse;
using AsistOff.MES.Configuration.Application.Features.Skills.Create;
using AsistOff.MES.Configuration.Application.Features.Skills.Delete;
using AsistOff.MES.Configuration.Application.Features.Skills.Get;
using AsistOff.MES.Configuration.Application.Features.Skills.Responses;
using AsistOff.MES.Configuration.Application.Features.Skills.Update;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/skills")]
public class SkillsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<SkillResponse>>> BrowseAsync(
        [FromQuery] BrowseSkillsRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SkillResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetSkillRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<SkillResponse>> CreateAsync(
        [FromBody] CreateSkillRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id, [FromBody] UpdateSkillRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteSkillRequest(id), cancellationToken);
        return NoContent();
    }
}
