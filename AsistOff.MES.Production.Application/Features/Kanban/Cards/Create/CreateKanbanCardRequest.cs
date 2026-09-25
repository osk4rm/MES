using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Kanban.Cards.Create;

public record CreateKanbanCardRequest(
    Guid LoopId,
    string? CardNumber,
    string? Notes) : ITenantRequest<KanbanCardResponse>;
