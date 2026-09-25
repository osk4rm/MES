using AsistOff.MES.Multitenancy.Contracts;
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

    /// <summary>
    /// Anonymous tenant self-registration. Per-IP throttled at the Gateway
    /// (see abuse-protection rate limiting). Returns only the minimal public
    /// projection (id, name, active status) — never secrets or settings.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<AnonymousTenantResponse>> CreateTenant(
        [FromBody] CreateTenantCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetTenant), new { id = result.Id }, result);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<TenantResponse>> GetTenant(Guid id)
    {
        var result = await _mediator.Send(new GetTenantQuery(id));
        return result;
    }
}
