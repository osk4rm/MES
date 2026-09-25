using AsistOff.MES.Production.Application.Features.OpcUaConnections;
using AsistOff.MES.Production.Application.Features.OpcUaConnections.Browse;
using AsistOff.MES.Production.Application.Features.OpcUaConnections.Create;
using AsistOff.MES.Production.Application.Features.OpcUaConnections.Delete;
using AsistOff.MES.Production.Application.Features.OpcUaConnections.Get;
using AsistOff.MES.Production.Application.Features.OpcUaConnections.Status;
using AsistOff.MES.Production.Application.Features.OpcUaConnections.Test;
using AsistOff.MES.Production.Application.Features.OpcUaConnections.Toggle;
using AsistOff.MES.Production.Application.Features.OpcUaConnections.Update;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/opcua-connections")]
public class OpcUaConnectionsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<OpcUaConnectionResponse>>> BrowseAsync(
        [FromQuery] BrowseOpcUaConnectionsRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("status")]
    public async Task<ActionResult<OpcUaConnectionStatusResponse>> StatusAsync(
        [FromQuery] GetOpcUaConnectionStatusRequest request, CancellationToken cancellationToken)
        => Ok(await sender.Send(request, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OpcUaConnectionResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetOpcUaConnectionRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<OpcUaConnectionResponse>> CreateAsync(
        [FromBody] CreateOpcUaConnectionRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id, [FromBody] UpdateOpcUaConnectionRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/toggle")]
    public async Task<ActionResult<OpcUaConnectionResponse>> ToggleAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new ToggleOpcUaConnectionRequest(id), cancellationToken));

    [HttpPost("{id:guid}/test")]
    public async Task<ActionResult<TestOpcUaConnectionResponse>> TestAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new TestOpcUaConnectionRequest(id), cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteOpcUaConnectionRequest(id), cancellationToken);
        return NoContent();
    }
}
