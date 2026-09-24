using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Kanban.Cards.Replenish;

public record ReplenishKanbanCardRequest(Guid Id) : ITenantRequest<KanbanCardResponse>;
