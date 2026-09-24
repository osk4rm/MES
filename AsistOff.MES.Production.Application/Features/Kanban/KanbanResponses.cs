using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.Kanban;

public record KanbanLoopResponse(
    Guid Id,
    string Code,
    Guid ProductId,
    Guid ConsumingMachineId,
    Guid SupplyingWarehouseId,
    decimal CardQuantity,
    int CardsInCirculation,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record KanbanCardResponse(
    Guid Id,
    Guid LoopId,
    string CardNumber,
    KanbanCardStatus Status,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
