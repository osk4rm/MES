using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.OperationOutputs.Update;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record UpdateOperationOutputRequest(
    Guid OutputId,
    Guid ProductId,
    Guid? MeasureUnitId,
    decimal Quantity,
    BomQuantityType QuantityType,
    OperationOutputType OutputType,
    Guid? PreferredWarehouseId,
    string? Notes,
    int SortIndex) : ITenantRequest;
