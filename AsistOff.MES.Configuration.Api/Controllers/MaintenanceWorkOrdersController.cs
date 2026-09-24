using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Browse;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Cancel;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Complete;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Create;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Get;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Start;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/maintenance-work-orders")]
public class MaintenanceWorkOrdersController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<MaintenanceWorkOrderResponse>>> BrowseAsync(
        [FromQuery] BrowseMaintenanceWorkOrdersRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaintenanceWorkOrderResponse>> GetAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetMaintenanceWorkOrderRequest(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<MaintenanceWorkOrderResponse>> CreateAsync(
        [FromBody] CreateMaintenanceWorkOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<MaintenanceWorkOrderResponse>> StartAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new StartMaintenanceWorkOrderRequest(id), cancellationToken));

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<MaintenanceWorkOrderResponse>> CompleteAsync(
        [FromRoute] Guid id, [FromBody] CompleteMaintenanceWorkOrderBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new CompleteMaintenanceWorkOrderRequest(id, body.ResolutionNotes), cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<MaintenanceWorkOrderResponse>> CancelAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new CancelMaintenanceWorkOrderRequest(id), cancellationToken));

    public sealed record CompleteMaintenanceWorkOrderBody(string? ResolutionNotes);
}
