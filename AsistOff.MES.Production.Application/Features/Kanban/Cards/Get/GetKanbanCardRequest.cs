using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Kanban.Cards.Get;

public record GetKanbanCardRequest(Guid Id) : ITenantRequest<KanbanCardResponse>;
