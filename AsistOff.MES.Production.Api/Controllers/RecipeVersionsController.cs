using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Application.Features.RecipeVersions.Clone;
using AsistOff.MES.Production.Application.Features.RecipeVersions.Create;
using AsistOff.MES.Production.Application.Features.RecipeVersions.Delete;
using AsistOff.MES.Production.Application.Features.RecipeVersions.Get;
using AsistOff.MES.Production.Application.Features.RecipeVersions.Release;
using AsistOff.MES.Production.Application.Features.RecipeVersions.UpdateMetadata;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/recipe-versions")]
public class RecipeVersionsController(ISender sender) : ApiController
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecipeVersionDetailResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetRecipeVersionRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<RecipeVersionDetailResponse>> CreateAsync(
        [FromBody] CreateRecipeVersionRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPost("clone")]
    public async Task<ActionResult<RecipeVersionDetailResponse>> CloneAsync(
        [FromBody] CloneRecipeVersionRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}/metadata")]
    public async Task<ActionResult> UpdateMetadataAsync(
        [FromRoute] Guid id, [FromBody] UpdateRecipeVersionMetadataRequest request, CancellationToken cancellationToken)
    {
        if (id != request.VersionId) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/release")]
    public async Task<ActionResult> ReleaseAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new ReleaseRecipeVersionRequest(id), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteRecipeVersionRequest(id), cancellationToken);
        return NoContent();
    }
}
