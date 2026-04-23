using AsistOff.MES.Configuration.Application.Features.Operators.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.Operators.Browse;

public record BrowseOperatorsRequest(
    string? Identifier,
    string? FirstName,
    string? LastName,
    decimal? RatePerHourFrom,
    decimal? RatePerHourTo,
    Guid? DepartmentId
) : IPagedRequest, ITenantRequest<PagedResponse<OperatorResponse>>
{
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["Identifier", "FirstName", "LastName", "RatePerHour"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public int? MaxPageSize => 100;
}
