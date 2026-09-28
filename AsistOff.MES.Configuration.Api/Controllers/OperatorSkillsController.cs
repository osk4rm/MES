using AsistOff.MES.Configuration.Application.Features.OperatorSkills.Assign;
using AsistOff.MES.Configuration.Application.Features.OperatorSkills.Browse;
using AsistOff.MES.Configuration.Application.Features.OperatorSkills.Get;
using AsistOff.MES.Configuration.Application.Features.OperatorSkills.Responses;
using AsistOff.MES.Configuration.Application.Features.OperatorSkills.Unassign;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/operator-skills")]
public class OperatorSkillsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<OperatorSkillQualificationResponse>>> BrowseAsync(
        [FromQuery] BrowseOperatorSkillsRequest request, CancellationToken cancellationToken)
        => Ok(await sender.Send(request, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OperatorSkillQualificationResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetOperatorSkillRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<OperatorSkillQualificationResponse>> AssignAsync(
        [FromBody] AssignOperatorSkillRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> UnassignAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new UnassignOperatorSkillRequest(id), cancellationToken);
        return NoContent();
    }
}
