using AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Browse;
using AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Create;
using AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Delete;
using AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Get;
using AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Responses;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/operator-shift-assignments")]
public class OperatorShiftAssignmentsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<OperatorShiftAssignmentResponse>>> BrowseAsync(
        [FromQuery] BrowseOperatorShiftAssignmentsRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OperatorShiftAssignmentResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetOperatorShiftAssignmentRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<OperatorShiftAssignmentResponse>> CreateAsync(
        [FromBody] CreateOperatorShiftAssignmentRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteOperatorShiftAssignmentRequest(id), cancellationToken);
        return NoContent();
    }
}
