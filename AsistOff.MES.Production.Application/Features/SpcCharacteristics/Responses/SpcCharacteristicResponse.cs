using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.SpcCharacteristics.Responses;

public record SpcCharacteristicResponse(
    Guid Id,
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
    bool IsActive);

public class PagedSpcCharacteristicsResponse(
    IReadOnlyCollection<SpcCharacteristicResponse> items,
    int totalCount,
    int? pageSize)
    : PagedResponse<SpcCharacteristicResponse>(items, totalCount, pageSize);
