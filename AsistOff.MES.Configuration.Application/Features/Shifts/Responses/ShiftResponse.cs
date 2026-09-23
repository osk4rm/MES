namespace AsistOff.MES.Configuration.Application.Features.Shifts.Responses;

public record ShiftResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsActive);

public class PagedShiftsResponse(IReadOnlyCollection<ShiftResponse> items, int totalCount, int? pageSize)
    : AsistOff.MES.Shared.Abstractions.Contracts.Paging.PagedResponse<ShiftResponse>(items, totalCount, pageSize);
