using AsistOff.MES.Configuration.Application.Features.MeasureUnits.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.MeasureUnits.Browse;

internal sealed class BrowseMeasureUnitsRequestHandler(
    IMeasureUnitsRepository measureUnitsRepository,
    ITenantContext tenantContext)
    : IRequestHandler<BrowseMeasureUnitsRequest, PagedResponse<MeasureUnitResponse>>
{
    public async Task<PagedResponse<MeasureUnitResponse>> Handle(BrowseMeasureUnitsRequest request,
        CancellationToken cancellationToken)
    {
        var filter = BuildPredicate(request);
        var totalCount = await measureUnitsRepository.CountAsync(cancellationToken);
        var paginator = new Paginator<MeasureUnit>(filter, request);
        
        var measureUnits = await measureUnitsRepository.BrowseAsync(paginator, cancellationToken);
        
        var items = measureUnits.Select(x => new MeasureUnitResponse(
                x.Id,
                x.Name,
                x.Symbol,
                x.Type,
                x.ConversionFactor,
                x.BaseUnitId,
                x.BaseUnit?.Name,
                x.IsActive,
                x.Description,
                x.SyncId
            )).ToList();

        return new PagedMeasureUnitsResponse(items, totalCount, request.PageSize);
    }
    
    private ExpressionStarter<MeasureUnit> BuildPredicate(BrowseMeasureUnitsRequest request)
    {
        var predicate = PredicateBuilder.New<MeasureUnit>(true);
        predicate = predicate.And(x => x.TenantId == tenantContext.TenantId);
        
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            predicate = predicate
                .And(x => x.Name.Contains(request.SearchTerm))
                .Or(x => x.Symbol.Contains(request.SearchTerm));
        }

        if (request.Type.HasValue)
        {
            predicate = predicate.And(x => x.Type == request.Type);
        }

        if (request.IsActive.HasValue)
        {
            predicate = predicate.And(x => x.IsActive == request.IsActive);
        }

        return predicate;
    }
}
