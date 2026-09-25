using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.Kanban.Cards.Create;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record CreateKanbanCardRequest(
    Guid LoopId,
    string? CardNumber,
    string? Notes) : ITenantRequest<KanbanCardResponse>;
