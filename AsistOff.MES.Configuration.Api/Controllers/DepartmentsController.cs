using AsistOff.MES.Configuration.Application.Features.Departments.Browse;
using AsistOff.MES.Configuration.Application.Features.Departments.Create;
using AsistOff.MES.Configuration.Application.Features.Departments.Delete;
using AsistOff.MES.Configuration.Application.Features.Departments.Get;
using AsistOff.MES.Configuration.Application.Features.Departments.Responses;
using AsistOff.MES.Configuration.Application.Features.Departments.Update;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/departments")]
public class DepartmentsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<DepartmentResponse>>> BrowseAsync(
        [FromQuery] BrowseDepartmentsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return result;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DepartmentResponse>> Get(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var request = new GetDepartmentRequest(id);
        var result = await sender.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<DepartmentResponse>> CreateAsync(
        [FromBody] CreateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        if (id != request.Id)
        {
            return BadRequest("Route ID does not match request ID");
        }

        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var request = new DeleteDepartmentRequest(id);
        await sender.Send(request, cancellationToken);
        return NoContent();
    }
}
