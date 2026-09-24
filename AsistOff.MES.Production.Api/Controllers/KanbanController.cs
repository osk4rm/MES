using AsistOff.MES.Production.Application.Features.Kanban;
using AsistOff.MES.Production.Application.Features.Kanban.Cards.Browse;
using AsistOff.MES.Production.Application.Features.Kanban.Cards.Create;
using AsistOff.MES.Production.Application.Features.Kanban.Cards.Delete;
using AsistOff.MES.Production.Application.Features.Kanban.Cards.Get;
using AsistOff.MES.Production.Application.Features.Kanban.Loops.Browse;
using AsistOff.MES.Production.Application.Features.Kanban.Loops.Create;
using AsistOff.MES.Production.Application.Features.Kanban.Loops.Delete;
using AsistOff.MES.Production.Application.Features.Kanban.Loops.Get;
using AsistOff.MES.Production.Application.Features.Kanban.Loops.Update;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Production.Api.Controllers;

[Route("api/kanban")]
public class KanbanController(ISender sender) : ApiController
{
    [HttpGet("loops")]
    public async Task<ActionResult<PagedResponse<KanbanLoopResponse>>> BrowseLoopsAsync(
        [FromQuery] BrowseKanbanLoopsRequest request, CancellationToken cancellationToken)
        => await sender.Send(request, cancellationToken);

    [HttpGet("loops/{id:guid}")]
    public async Task<ActionResult<KanbanLoopResponse>> GetLoopAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetKanbanLoopRequest(id), cancellationToken));

    [HttpPost("loops")]
    public async Task<ActionResult<KanbanLoopResponse>> CreateLoopAsync(
        [FromBody] CreateKanbanLoopRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return CreatedAtAction("GetLoop", new { id = result.Id }, result);
    }

    [HttpPut("loops/{id:guid}")]
    public async Task<ActionResult> UpdateLoopAsync(
        [FromRoute] Guid id, [FromBody] UpdateKanbanLoopBody body, CancellationToken cancellationToken)
    {
        await sender.Send(new UpdateKanbanLoopRequest(id, body.CardQuantity, body.CardsInCirculation, body.IsActive, body.Notes), cancellationToken);
        return NoContent();
    }

    [HttpDelete("loops/{id:guid}")]
    public async Task<ActionResult> DeleteLoopAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteKanbanLoopRequest(id), cancellationToken);
        return NoContent();
    }

    [HttpGet("loops/{loopId:guid}/cards")]
    public async Task<ActionResult<PagedResponse<KanbanCardResponse>>> BrowseCardsAsync(
        [FromRoute] Guid loopId, [FromQuery] BrowseKanbanCardsRequest request, CancellationToken cancellationToken)
    {
        request.LoopId = loopId;
        return await sender.Send(request, cancellationToken);
    }

    [HttpGet("loops/{loopId:guid}/cards/{id:guid}")]
    public async Task<ActionResult<KanbanCardResponse>> GetCardAsync(
        [FromRoute] Guid loopId, [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var card = await sender.Send(new GetKanbanCardRequest(id), cancellationToken);
        if (card.LoopId != loopId)
            throw new NotFoundException("KanbanCard", id);

        return Ok(card);
    }

    [HttpPost("loops/{loopId:guid}/cards")]
    public async Task<ActionResult<KanbanCardResponse>> CreateCardAsync(
        [FromRoute] Guid loopId, [FromBody] CreateKanbanCardBody body, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateKanbanCardRequest(loopId, body.CardNumber, body.Notes), cancellationToken);
        return CreatedAtAction("GetCard", new { loopId = result.LoopId, id = result.Id }, result);
    }

    [HttpDelete("loops/{loopId:guid}/cards/{id:guid}")]
    public async Task<ActionResult> DeleteCardAsync(
        [FromRoute] Guid loopId, [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var card = await sender.Send(new GetKanbanCardRequest(id), cancellationToken);
        if (card.LoopId != loopId)
            throw new NotFoundException("KanbanCard", id);

        await sender.Send(new DeleteKanbanCardRequest(id), cancellationToken);
        return NoContent();
    }

    public sealed record UpdateKanbanLoopBody(
        decimal CardQuantity,
        int CardsInCirculation,
        bool IsActive,
        string? Notes);

    public sealed record CreateKanbanCardBody(
        string? CardNumber,
        string? Notes);
}
