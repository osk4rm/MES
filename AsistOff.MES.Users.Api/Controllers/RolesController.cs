using AsistOff.MES.Shared.Infrastructure.Controllers;
using AsistOff.MES.Users.Application.Features.Roles.Assign;
using AsistOff.MES.Users.Application.Features.Roles.Browse;
using AsistOff.MES.Users.Application.Features.Roles.Create;
using AsistOff.MES.Users.Application.Features.Roles.Get;
using AsistOff.MES.Users.Application.Features.Roles.Responses;
using AsistOff.MES.Users.Application.Features.Roles.SetPermissions;
using AsistOff.MES.Users.Application.Features.Roles.Unassign;
using AsistOff.MES.Users.Application.Features.Roles.Update;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Users.Api.Controllers;

[Route("api/roles")]
public class RolesController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<RoleResponse>>> BrowseAsync(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new BrowseRolesRequest(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RoleDetailResponse>> GetAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRoleRequest(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<RoleResponse>> CreateAsync(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction(nameof(GetAsync), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (id != request.Id)
        {
            return BadRequest("Route ID does not match request ID");
        }

        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/permissions")]
    public async Task<ActionResult> SetPermissionsAsync(
        [FromRoute] Guid id,
        [FromBody] SetRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        if (id != request.RoleId)
        {
            return BadRequest("Route ID does not match request role ID");
        }

        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult> AssignMemberAsync(
        [FromRoute] Guid id,
        [FromBody] AssignUserToRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (id != request.RoleId)
        {
            return BadRequest("Route ID does not match request role ID");
        }

        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<ActionResult> UnassignMemberAsync(
        [FromRoute] Guid id,
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UnassignUserFromRoleRequest(id, userId), cancellationToken);
        return NoContent();
    }
}
