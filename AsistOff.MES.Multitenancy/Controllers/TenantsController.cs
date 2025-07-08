using AsistOff.MES.Multitenancy.Requests.Commands.Create;
using AsistOff.MES.Multitenancy.Requests.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Multitenancy.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TenantsController : ControllerBase
{
    private readonly IMediator _mediator;
    public TenantsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetTenant), new { id = result.Id }, result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTenant(Guid id)
    {
        var result = await _mediator.Send(new GetTenantQuery(id));
        if (result == null) return NotFound();
        return Ok(result);
    }
}

