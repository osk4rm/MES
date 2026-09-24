using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Kanban.Cards.Delete;

public record DeleteKanbanCardRequest(Guid Id) : ITenantRequest;
