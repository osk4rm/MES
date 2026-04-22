using AsistOff.MES.Configuration.Application.Features.MeasureUnits.Responses;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.MeasureUnits.Browse;

public record BrowseMeasureUnitsRequest(
    string? SearchTerm = null,
    MeasureUnitType? Type = null,
    bool? IsActive = null
) : ITenantRequest<PagedResponse<MeasureUnitResponse>>, IPagedRequest
{
    public List<string> RawSort { get; set; } = new();

    public IReadOnlyCollection<string> SupportedSortFields { get; } =
        ["Name", "Symbol", "Type", "ConversionFactor", "BaseUnitId", "IsActive", "Description"];

    public int? PageNumber { get; } = 1;
    public int? PageSize { get; } = 20;
    public int? MaxPageSize { get; } = 100;
}