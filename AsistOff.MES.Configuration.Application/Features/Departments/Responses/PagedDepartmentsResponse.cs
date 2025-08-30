using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.Departments.Responses;

public class PagedDepartmentsResponse(
    IReadOnlyCollection<DepartmentResponse> items,
    int totalCount,
    int? pageSize)
    : PagedResponse<DepartmentResponse>(items, totalCount, pageSize);
