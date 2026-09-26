using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Browse;
using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Create;
using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Delete;
using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Due;
using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.EvaluateDue;
using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Get;
using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.RaiseNow;
using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Responses;
using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Update;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/maintenance-plans")]
public class MaintenancePlansController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<MaintenancePlanResponse>>> BrowseAsync(
        [FromQuery] BrowseMaintenancePlansRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("due")]
    public async Task<ActionResult<IReadOnlyCollection<MaintenancePlanResponse>>> DueAsync(
        [FromQuery] GetDueMaintenancePlansRequest request, CancellationToken cancellationToken)
        => Ok(await sender.Send(request, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaintenancePlanResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetMaintenancePlanRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<MaintenancePlanResponse>> CreateAsync(
        [FromBody] CreateMaintenancePlanRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] Guid id, [FromBody] UpdateMaintenancePlanRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route ID does not match request ID");
        await sender.Send(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteMaintenancePlanRequest(id), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Evaluate due preventive plans and raise one Open work order per due
    /// plan. Time plans use <c>NextDueAt</c>; Meter plans use the supplied
    /// meter reading (body or query). Idempotent: plans with a live
    /// Open/InProgress order are skipped.
    /// </summary>
    [HttpPost("evaluate-due")]
    public async Task<ActionResult<IReadOnlyCollection<MaintenanceWorkOrderResponse>>> EvaluateDueAsync(
        [FromBody] EvaluateDueMaintenancePlansRequest? body,
        [FromQuery] decimal? currentMeterReading,
        [FromQuery] decimal? meterReading,
        CancellationToken cancellationToken)
    {
        var request = body ?? new EvaluateDueMaintenancePlansRequest();
        if (!request.CurrentMeterReading.HasValue && currentMeterReading.HasValue)
            request = request with { CurrentMeterReading = currentMeterReading };
        if (!request.MeterReading.HasValue && meterReading.HasValue)
            request = request with { MeterReading = meterReading };

        return Ok(await sender.Send(request, cancellationToken));
    }

    /// <summary>
    /// Manually raise one Open work order from a single plan. Idempotent:
    /// returns the existing live order instead of duplicating it.
    /// </summary>
    [HttpPost("{id:guid}/raise-now")]
    public async Task<ActionResult<MaintenanceWorkOrderResponse>> RaiseNowAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new RaiseMaintenancePlanNowRequest(id), cancellationToken));
}
