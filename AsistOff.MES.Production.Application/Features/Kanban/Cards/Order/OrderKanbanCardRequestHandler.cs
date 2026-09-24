using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Kanban.Cards.Order;

internal sealed class OrderKanbanCardRequestHandler(
    IKanbanCardsRepository cardsRepository,
    IKanbanLoopsRepository loopsRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<OrderKanbanCardRequest, KanbanCardResponse>
{
    public async Task<KanbanCardResponse> Handle(OrderKanbanCardRequest request, CancellationToken cancellationToken)
    {
        // The global tenant query filter scopes these lookups to the caller tenant,
        // so a cross-tenant card id resolves to 404, never to data.
        var card = await cardsRepository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("KanbanCard", request.Id);

        if (card.Status != KanbanCardStatus.Empty)
            throw new ConflictException($"Kanban card '{card.CardNumber}' must be in Empty status to be ordered.");

        var loop = await loopsRepository.GetAsync(card.LoopId, cancellationToken)
            ?? throw new NotFoundException("KanbanLoop", card.LoopId);

        var orderedPredicate = PredicateBuilder.New<KanbanCard>(true)
            .And(x => x.LoopId == card.LoopId)
            .And(x => x.Status == KanbanCardStatus.Ordered);
        var orderedCount = await cardsRepository.CountAsync(orderedPredicate, cancellationToken);
        if (orderedCount >= loop.CardsInCirculation)
            throw new ConflictException($"Kanban loop '{loop.Code}' already has {orderedCount} ordered cards (limit {loop.CardsInCirculation}).");

        card.Status = KanbanCardStatus.Ordered;
        card.UpdatedAt = dateTimeProvider.UtcNow;

        await cardsRepository.UpdateAsync(card, cancellationToken);
        return KanbanMappings.Map(card);
    }
}
