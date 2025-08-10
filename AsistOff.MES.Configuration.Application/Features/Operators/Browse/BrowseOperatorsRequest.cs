using AsistOff.MES.Configuration.Application.Features.Operators.Responses;
using AsistOff.MES.Multitenancy.Requests;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using ErrorOr;

namespace AsistOff.MES.Configuration.Application.Features.Operators.Browse;

public record BrowseOperatorsRequest : IPagedRequest, ITenantRequest<ErrorOr<PagedResponse<OperatorResponse>>>
{
    public string Identifier { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public decimal? RatePerHourFrom { get; set; }
    public decimal? RatePerHourTo { get; set; }
    public Guid DepartmentId { get; set; }
    public IReadOnlyCollection<string> RawSort { get; } = [];

    public IReadOnlyCollection<string> SupportedSortFields { get; } = new List<string>
        { "identifier", "first_name", "last_name", "rate_per_hour" };

    public int? PageNumber { get; set; }
    public int? PageSize { get; set; }
    public int? MaxPageSize { get; }
}