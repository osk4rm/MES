using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.Kanban.Cards.Consume;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record ConsumeKanbanCardRequest(Guid Id) : ITenantRequest<KanbanCardResponse>;
