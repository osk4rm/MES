using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.TelemetryReadings;

public class PagedTelemetryReadingsResponse(
    IReadOnlyCollection<TelemetryReadingResponse> items, int totalCount, int? pageSize)
    : PagedResponse<TelemetryReadingResponse>(items, totalCount, pageSize);
