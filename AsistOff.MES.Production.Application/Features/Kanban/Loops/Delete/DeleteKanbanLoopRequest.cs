using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Kanban.Loops.Delete;

public record DeleteKanbanLoopRequest(Guid Id) : ITenantRequest;
