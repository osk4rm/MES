using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.Kanban.Cards.Replenish;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record ReplenishKanbanCardRequest(Guid Id) : ITenantRequest<KanbanCardResponse>;
