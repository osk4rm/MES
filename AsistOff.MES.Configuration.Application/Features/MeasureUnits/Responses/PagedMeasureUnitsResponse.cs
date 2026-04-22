using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.MeasureUnits.Responses;

public class PagedMeasureUnitsResponse(IReadOnlyCollection<MeasureUnitResponse> items, int totalCount, int? pageSize)
    : PagedResponse<MeasureUnitResponse>(items, totalCount, pageSize);