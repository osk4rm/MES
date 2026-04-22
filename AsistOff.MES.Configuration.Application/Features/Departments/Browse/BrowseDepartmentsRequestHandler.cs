using AsistOff.MES.Configuration.Application.Features.Departments.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Departments.Browse;

internal sealed class BrowseDepartmentsRequestHandler(
    IDepartmentsRepository departmentsRepository,
    ITenantContext tenantContext)
    : IRequestHandler<BrowseDepartmentsRequest, PagedResponse<DepartmentResponse>>
{
    public async Task<PagedResponse<DepartmentResponse>> Handle(BrowseDepartmentsRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<Department>(true)
            .And(x => x.TenantId == tenantContext.TenantId);

        if (!string.IsNullOrWhiteSpace(request.Name))
            predicate = predicate.And(x => x.Name.Contains(request.Name));

        if (!string.IsNullOrWhiteSpace(request.Code))
            predicate = predicate.And(x => x.Code.Contains(request.Code));

        var totalCount = await departmentsRepository.CountAsync(cancellationToken);
        var paginator = new Paginator<Department>(predicate, request);

        var departments = await departmentsRepository.BrowseAsync(paginator, cancellationToken);

        var items = departments
            .Select(d => new DepartmentResponse
            {
                Id = d.Id,
                Code = d.Code,
                Name = d.Name
            })
            .ToList();

        return new PagedDepartmentsResponse(items, totalCount, request.PageSize);
    }
}