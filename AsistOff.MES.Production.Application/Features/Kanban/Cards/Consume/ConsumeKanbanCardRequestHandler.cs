using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Kanban.Cards.Consume;

internal sealed class ConsumeKanbanCardRequestHandler(
    IKanbanCardsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ConsumeKanbanCardRequest, KanbanCardResponse>
{
    public async Task<KanbanCardResponse> Handle(ConsumeKanbanCardRequest request, CancellationToken cancellationToken)
    {
        // The global tenant query filter scopes this lookup to the caller tenant,
        // so a cross-tenant card id resolves to 404, never to data.
        var card = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("KanbanCard", request.Id);

        if (card.Status != KanbanCardStatus.Full)
            throw new ConflictException($"Kanban card '{card.CardNumber}' must be in Full status to be consumed.");

        card.Status = KanbanCardStatus.Empty;
        card.UpdatedAt = dateTimeProvider.UtcNow;

        await repository.UpdateAsync(card, cancellationToken);
        return KanbanMappings.Map(card);
    }
}
