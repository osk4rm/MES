using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.SpcMeasurements;

public record SpcMeasurementResponse(
    Guid Id,
    Guid CharacteristicId,
    decimal Value,
    DateTime MeasuredAt,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public class PagedSpcMeasurementsResponse(
    IReadOnlyCollection<SpcMeasurementResponse> items,
    int totalCount,
    int? pageSize)
    : PagedResponse<SpcMeasurementResponse>(items, totalCount, pageSize);
