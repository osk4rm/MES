using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.Lots.Create;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record CreateLotRequest(
    string Code,
    Guid ProductId,
    Guid MeasureUnitId,
    decimal Quantity,
    string? SupplierLotNumber,
    DateTime? ProducedAt,
    DateTime? ExpiryDate,
    string? Notes) : ITenantRequest<LotResponse>;
