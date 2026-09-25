using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Kanban.Loops.Create;

public record CreateKanbanLoopRequest(
    string Code,
    Guid ProductId,
    Guid ConsumingMachineId,
    Guid SupplyingWarehouseId,
    decimal CardQuantity,
    int CardsInCirculation,
    string? Notes) : ITenantRequest<KanbanLoopResponse>;
