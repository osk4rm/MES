using AsistOff.MES.Shared.Infrastructure.Controllers;
using AsistOff.MES.Users.Application.Features.Permissions.Browse;
using AsistOff.MES.Users.Application.Features.Roles.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Users.Api.Controllers;

[Route("api/permissions")]
public class PermissionsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PermissionResponse>>> BrowseAsync(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new BrowsePermissionsRequest(), cancellationToken);
        return Ok(result);
    }
}
