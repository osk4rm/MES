using AsistOff.MES.Multitenancy.Requests.Commands.Create;
using AsistOff.MES.Multitenancy.Requests.Queries;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Multitenancy.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TenantsController : ApiController
{
    private readonly IMediator _mediator;
    public TenantsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantCommand command)
    {
        var result = await _mediator.Send(command);

        return result.Match(
            onValue: x => CreatedAtAction(nameof(GetTenant), new { id = x }, x),
            onError: Problem);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTenant(Guid id)
    {
        var result = await _mediator.Send(new GetTenantQuery(id));
        return result.Match(
            onValue: Ok,
            onError: Problem);
    }
}

