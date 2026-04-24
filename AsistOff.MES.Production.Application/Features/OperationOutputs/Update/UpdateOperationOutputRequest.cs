using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.OperationOutputs.Update;

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
