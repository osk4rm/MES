using AsistOff.MES.Configuration.Application.Features.MeasureUnits.Responses;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.MeasureUnits.Create;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record CreateMeasureUnitRequest(
    string Name,
    string Symbol,
    MeasureUnitType Type,
    decimal? ConversionFactor,
    Guid? BaseUnitId,
    bool IsActive,
    string? Description,
    string? SyncId
) : ITenantRequest<MeasureUnitResponse>;
