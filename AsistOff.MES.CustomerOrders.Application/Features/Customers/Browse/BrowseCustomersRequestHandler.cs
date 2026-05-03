using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.CustomerOrders.Domain.Entities;
using AsistOff.MES.CustomerOrders.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.CustomerOrders.Application.Features.Customers.Browse;

internal sealed class BrowseCustomersRequestHandler(ICustomersRepository repository)
    : IRequestHandler<BrowseCustomersRequest, PagedResponse<CustomerResponse>>
{
    public async Task<PagedResponse<CustomerResponse>> Handle(BrowseCustomersRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<Customer>(true);
        if (!string.IsNullOrWhiteSpace(request.Code)) predicate = predicate.And(x => x.Code.Contains(request.Code));
        if (!string.IsNullOrWhiteSpace(request.Name)) predicate = predicate.And(x => x.Name.Contains(request.Name));
        if (!string.IsNullOrWhiteSpace(request.TaxId)) predicate = predicate.And(x => x.TaxId != null && x.TaxId.Contains(request.TaxId));
        if (request.IsActive.HasValue) predicate = predicate.And(x => x.IsActive == request.IsActive.Value);

        var total = await repository.CountAsync(predicate, cancellationToken);
        var items = await repository.BrowseAsync(new Paginator<Customer>(predicate, request), cancellationToken);
        return new PagedCustomersResponse(items.Select(CustomerOrderMappers.Map).ToList(), total, request.PageSize);
    }
}
