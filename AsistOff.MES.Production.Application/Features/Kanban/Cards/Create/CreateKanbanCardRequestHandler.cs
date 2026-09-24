using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Kanban;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Kanban.Cards.Create;

internal sealed class CreateKanbanCardRequestHandler(
    IKanbanLoopsRepository loopsRepository,
    IKanbanCardsRepository cardsRepository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateKanbanCardRequest, KanbanCardResponse>
{
    public async Task<KanbanCardResponse> Handle(CreateKanbanCardRequest request, CancellationToken cancellationToken)
    {
        // The global tenant query filter scopes this lookup to the caller tenant,
        // so a cross-tenant loop id resolves to 404, never to data.
        var loop = await loopsRepository.GetAsync(request.LoopId, cancellationToken)
            ?? throw new NotFoundException("KanbanLoop", request.LoopId);

        if (request.Notes is { Length: > 1000 })
            throw new ValidationException(nameof(request.Notes), "Notes must not exceed 1000 characters.");

        var cardNumber = request.CardNumber;
        if (string.IsNullOrWhiteSpace(cardNumber))
            cardNumber = await GenerateCardNumberAsync(loop, cancellationToken);

        if (cardNumber.Length > 50)
            throw new ValidationException(nameof(request.CardNumber), "Card number must not exceed 50 characters.");

        if (await cardsRepository.CardNumberExistsAsync(loop.Id, cardNumber, null, cancellationToken))
            throw new ConflictException($"Kanban card with number '{cardNumber}' already exists in loop '{loop.Code}'.");

        var card = new KanbanCard
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            LoopId = loop.Id,
            CardNumber = cardNumber,
            Status = KanbanCardStatus.Full,
            Notes = request.Notes,
            CreatedAt = dateTimeProvider.UtcNow
        };

        await cardsRepository.AddAsync(card, cancellationToken);
        return KanbanMappings.Map(card);
    }

    private async Task<string> GenerateCardNumberAsync(KanbanLoop loop, CancellationToken cancellationToken)
    {
        var sequence = await cardsRepository.CountByLoopAsync(loop.Id, cancellationToken) + 1;
        var candidate = $"{loop.Code}-{sequence:00}";

        while (await cardsRepository.CardNumberExistsAsync(loop.Id, candidate, null, cancellationToken))
        {
            sequence++;
            candidate = $"{loop.Code}-{sequence:00}";
        }

        return candidate;
    }
}
