using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.SpcCharacteristics.Responses;
using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.SpcCharacteristics.Create;

public record CreateSpcCharacteristicRequest(
    string Code,
    string Name,
    string? Description,
    Guid? ProductId,
    Guid? MachineId,
    SpcChartType ChartType,
    decimal? NominalValue,
    decimal? LowerSpecLimit,
    decimal? UpperSpecLimit,
    decimal? LowerControlLimit,
    decimal? UpperControlLimit,
    int SampleSize,
    string? Unit,
    bool IsActive) : ITenantRequest<SpcCharacteristicResponse>;
