using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Application.Features.Recipes.Browse;
using AsistOff.MES.Production.Application.Features.Recipes.Create;
using AsistOff.MES.Production.Application.Features.Recipes.Delete;
using AsistOff.MES.Production.Application.Features.Recipes.Get;
using AsistOff.MES.Production.Application.Features.Recipes.Update;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/recipes")]
public class RecipesController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<RecipeResponse>>> BrowseAsync(
        [FromQuery] BrowseRecipesRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecipeResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetRecipeRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<RecipeResponse>> CreateAsync(
        [FromBody] CreateRecipeRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id, [FromBody] UpdateRecipeRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteRecipeRequest(id), cancellationToken);
        return NoContent();
    }
}
