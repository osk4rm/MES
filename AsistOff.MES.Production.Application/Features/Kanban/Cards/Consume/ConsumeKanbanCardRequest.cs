using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Kanban.Cards.Consume;

public record ConsumeKanbanCardRequest(Guid Id) : ITenantRequest<KanbanCardResponse>;
