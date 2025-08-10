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

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<Guid>> CreateTenant([FromBody] CreateTenantCommand command)
    {
        var result = await _mediator.Send(command);

        return result;
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<TenantResponse>> GetTenant(Guid id)
    {
        var result = await _mediator.Send(new GetTenantQuery(id));
        return result;
    }
}

