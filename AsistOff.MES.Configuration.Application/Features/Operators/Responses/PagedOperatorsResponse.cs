using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.Operators.Responses;

public class PagedOperatorsResponse(IReadOnlyCollection<OperatorResponse> items, int totalCount, int? pageSize)
    : PagedResponse<OperatorResponse>(items, totalCount, pageSize);