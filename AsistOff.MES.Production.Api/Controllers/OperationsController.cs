using AsistOff.MES.Production.Application.Features.BomItems.Add;
using AsistOff.MES.Production.Application.Features.BomItems.Remove;
using AsistOff.MES.Production.Application.Features.BomItems.Update;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Application.Features.OperationDependencies.Set;
using AsistOff.MES.Production.Application.Features.OperationOutputs.Add;
using AsistOff.MES.Production.Application.Features.OperationOutputs.Remove;
using AsistOff.MES.Production.Application.Features.OperationOutputs.Update;
using AsistOff.MES.Production.Application.Features.Operations.Add;
using AsistOff.MES.Production.Application.Features.Operations.Delete;
using AsistOff.MES.Production.Application.Features.Operations.Reorder;
using AsistOff.MES.Production.Application.Features.Operations.Update;
using AsistOff.MES.Production.Application.Features.ResourceRequirements.Add;
using AsistOff.MES.Production.Application.Features.ResourceRequirements.Remove;
using AsistOff.MES.Production.Application.Features.ResourceRequirements.Update;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api")]
public class OperationsController(ISender sender) : ApiController
{
    // ----- operations -----

    [HttpPost("operations")]
    public async Task<ActionResult<OperationNodeResponse>> AddOperationAsync(
        [FromBody] AddOperationRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("operations/{id:guid}")]
    public async Task<ActionResult> UpdateOperationAsync(
        [FromRoute] Guid id, [FromBody] UpdateOperationRequest request, CancellationToken cancellationToken)
    {
        if (id != request.OperationId) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("operations/{id:guid}")]
    public async Task<ActionResult> DeleteOperationAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteOperationRequest(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("recipe-versions/{versionId:guid}/operations/reorder")]
    public async Task<ActionResult> ReorderOperationsAsync(
        [FromRoute] Guid versionId,
        [FromBody] ReorderOperationsBody body,
        CancellationToken cancellationToken)
    {
        await sender.Send(new ReorderOperationsRequest(versionId, body.Order), cancellationToken);
        return NoContent();
    }

    public record ReorderOperationsBody(IReadOnlyCollection<OperationSortEntry> Order);

    // ----- dependencies -----

    [HttpPut("operations/{id:guid}/dependencies")]
    public async Task<ActionResult> SetDependenciesAsync(
        [FromRoute] Guid id,
        [FromBody] SetDependenciesBody body,
        CancellationToken cancellationToken)
    {
        await sender.Send(new SetOperationDependenciesRequest(id, body.Dependencies), cancellationToken);
        return NoContent();
    }

    public record SetDependenciesBody(IReadOnlyCollection<DependencyEntry> Dependencies);

    // ----- bom items -----

    [HttpPost("operations/{id:guid}/bom-items")]
    public async Task<ActionResult<Guid>> AddBomItemAsync(
        [FromRoute] Guid id, [FromBody] AddBomItemRequest request, CancellationToken cancellationToken)
    {
        if (id != request.OperationId) return BadRequest("Route ID does not match request ID");
        var result = await sender.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("bom-items/{id:guid}")]
    public async Task<ActionResult> UpdateBomItemAsync(
        [FromRoute] Guid id, [FromBody] UpdateBomItemRequest request, CancellationToken cancellationToken)
    {
        if (id != request.BomItemId) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("bom-items/{id:guid}")]
    public async Task<ActionResult> RemoveBomItemAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new RemoveBomItemRequest(id), cancellationToken);
        return NoContent();
    }

    // ----- outputs -----

    [HttpPost("operations/{id:guid}/outputs")]
    public async Task<ActionResult<Guid>> AddOutputAsync(
        [FromRoute] Guid id, [FromBody] AddOperationOutputRequest request, CancellationToken cancellationToken)
    {
        if (id != request.OperationId) return BadRequest("Route ID does not match request ID");
        var result = await sender.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("outputs/{id:guid}")]
    public async Task<ActionResult> UpdateOutputAsync(
        [FromRoute] Guid id, [FromBody] UpdateOperationOutputRequest request, CancellationToken cancellationToken)
    {
        if (id != request.OutputId) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("outputs/{id:guid}")]
    public async Task<ActionResult> RemoveOutputAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new RemoveOperationOutputRequest(id), cancellationToken);
        return NoContent();
    }

    // ----- resource requirements -----

    [HttpPost("operations/{id:guid}/resources")]
    public async Task<ActionResult<Guid>> AddResourceAsync(
        [FromRoute] Guid id, [FromBody] AddResourceRequirementRequest request, CancellationToken cancellationToken)
    {
        if (id != request.OperationId) return BadRequest("Route ID does not match request ID");
        var result = await sender.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("resources/{id:guid}")]
    public async Task<ActionResult> UpdateResourceAsync(
        [FromRoute] Guid id, [FromBody] UpdateResourceRequirementRequest request, CancellationToken cancellationToken)
    {
        if (id != request.ResourceRequirementId) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("resources/{id:guid}")]
    public async Task<ActionResult> RemoveResourceAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new RemoveResourceRequirementRequest(id), cancellationToken);
        return NoContent();
    }
}
