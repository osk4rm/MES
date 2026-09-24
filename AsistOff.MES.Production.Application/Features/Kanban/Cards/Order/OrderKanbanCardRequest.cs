using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Kanban.Cards.Order;

public record OrderKanbanCardRequest(Guid Id) : ITenantRequest<KanbanCardResponse>;
