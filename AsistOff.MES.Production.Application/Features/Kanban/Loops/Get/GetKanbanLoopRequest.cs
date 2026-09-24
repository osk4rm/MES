using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Kanban.Loops.Get;

public record GetKanbanLoopRequest(Guid Id) : ITenantRequest<KanbanLoopResponse>;
