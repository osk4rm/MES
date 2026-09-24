using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.AndonSignals;

public class PagedAndonSignalsResponse(
    IReadOnlyCollection<AndonSignalResponse> items, int totalCount, int? pageSize)
    : PagedResponse<AndonSignalResponse>(items, totalCount, pageSize);
