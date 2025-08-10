using AsistOff.MES.Configuration.Application.Features.Operators.Responses;
using AsistOff.MES.Multitenancy.Requests;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.Operators.Browse;

public record BrowseOperatorsRequest(
    string? Identifier,
    string? FirstName,
    string? LastName,
    decimal? RatePerHourFrom,
    decimal? RatePerHourTo,
    Guid DepartmentId
) : IPagedRequest, ITenantRequest<PagedResponse<OperatorResponse>>
{
    public IReadOnlyCollection<string> RawSort { get; } = [];
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["Identifier", "FirstName", "LastName", "RatePerHour"];
    public int? PageNumber { get; }
    public int? PageSize { get; }
    public int? MaxPageSize { get; }
}
