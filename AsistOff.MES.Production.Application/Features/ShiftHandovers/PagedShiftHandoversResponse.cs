using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.ShiftHandovers;

public class PagedShiftHandoversResponse(
    IReadOnlyCollection<ShiftHandoverResponse> items, int totalCount, int? pageSize)
    : PagedResponse<ShiftHandoverResponse>(items, totalCount, pageSize);
