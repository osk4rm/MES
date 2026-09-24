using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Kanban.Loops.Update;

public record UpdateKanbanLoopRequest(
    Guid Id,
    decimal CardQuantity,
    int CardsInCirculation,
    bool IsActive,
    string? Notes) : ITenantRequest;
