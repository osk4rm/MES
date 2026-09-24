using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Kanban.Cards.Replenish;

internal sealed class ReplenishKanbanCardRequestHandler(
    IKanbanCardsRepository cardsRepository,
    IKanbanLoopsRepository loopsRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ReplenishKanbanCardRequest, KanbanCardResponse>
{
    public async Task<KanbanCardResponse> Handle(ReplenishKanbanCardRequest request, CancellationToken cancellationToken)
    {
        // The global tenant query filter scopes these lookups to the caller tenant,
        // so a cross-tenant card id resolves to 404, never to data.
        var card = await cardsRepository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("KanbanCard", request.Id);

        if (card.Status != KanbanCardStatus.Ordered)
            throw new ConflictException($"Kanban card '{card.CardNumber}' must be in Ordered status to be replenished.");

        var loop = await loopsRepository.GetAsync(card.LoopId, cancellationToken)
            ?? throw new NotFoundException("KanbanLoop", card.LoopId);

        if (!loop.IsActive)
            throw new ConflictException($"Kanban loop '{loop.Code}' is inactive; ordered cards cannot be replenished.");

        card.Status = KanbanCardStatus.Full;
        card.UpdatedAt = dateTimeProvider.UtcNow;

        await cardsRepository.UpdateAsync(card, cancellationToken);
        return KanbanMappings.Map(card);
    }
}
