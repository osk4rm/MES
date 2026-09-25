using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.Kanban.Loops.Create;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record CreateKanbanLoopRequest(
    string Code,
    Guid ProductId,
    Guid ConsumingMachineId,
    Guid SupplyingWarehouseId,
    decimal CardQuantity,
    int CardsInCirculation,
    string? Notes) : ITenantRequest<KanbanLoopResponse>;
