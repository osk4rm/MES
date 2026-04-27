using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.BomItems.Add;

public record AddBomItemRequest(
    Guid OperationId,
    Guid ProductId,
    Guid? MeasureUnitId,
    decimal Quantity,
    BomQuantityType QuantityType,
    decimal? ScrapPercentage,
    bool IsOptional,
    Guid? PreferredWarehouseId,
    ConsumptionTiming ConsumptionTiming,
    string? Notes,
    int? SortIndex) : ITenantRequest<Guid>;
