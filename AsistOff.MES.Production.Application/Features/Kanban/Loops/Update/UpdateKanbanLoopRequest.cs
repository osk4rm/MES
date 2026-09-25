using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.Kanban.Loops.Update;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record UpdateKanbanLoopRequest(
    Guid Id,
    decimal CardQuantity,
    int CardsInCirculation,
    bool IsActive,
    string? Notes) : ITenantRequest;
