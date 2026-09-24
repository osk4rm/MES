using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Exceptions;

namespace AsistOff.MES.Production.Application.Features.Kanban;

internal static class KanbanMappings
{
    internal static KanbanLoopResponse Map(KanbanLoop loop) => new(
        loop.Id,
        loop.Code,
        loop.ProductId,
        loop.ConsumingMachineId,
        loop.SupplyingWarehouseId,
        loop.CardQuantity,
        loop.CardsInCirculation,
        loop.IsActive,
        loop.Notes,
        loop.CreatedAt,
        loop.UpdatedAt);

    internal static KanbanCardResponse Map(KanbanCard card) => new(
        card.Id,
        card.LoopId,
        card.CardNumber,
        card.Status,
        card.Notes,
        card.CreatedAt,
        card.UpdatedAt);

    internal static void ValidateLoopInput(string code, decimal cardQuantity, int cardsInCirculation, string? notes)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ValidationException(nameof(code), "Code is required.");

        if (code.Length > 50)
            throw new ValidationException(nameof(code), "Code must not exceed 50 characters.");

        ValidateLoopQuantities(cardQuantity, cardsInCirculation);

        if (notes is { Length: > 1000 })
            throw new ValidationException(nameof(notes), "Notes must not exceed 1000 characters.");
    }

    internal static void ValidateLoopQuantities(decimal cardQuantity, int cardsInCirculation)
    {
        if (cardQuantity <= 0)
            throw new ValidationException(nameof(cardQuantity), "Card quantity must be greater than zero.");

        if (cardsInCirculation < 1 || cardsInCirculation > 100)
            throw new ValidationException(nameof(cardsInCirculation), "Cards in circulation must be between 1 and 100.");
    }
}
