using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.CustomerOrders.Domain.Entities;
using AsistOff.MES.CustomerOrders.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Browse;

internal sealed class BrowseCustomerOrdersRequestHandler(ICustomerOrdersRepository repository)
    : IRequestHandler<BrowseCustomerOrdersRequest, PagedResponse<CustomerOrderResponse>>
{
    public async Task<PagedResponse<CustomerOrderResponse>> Handle(BrowseCustomerOrdersRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<CustomerOrder>(true);
        if (!string.IsNullOrWhiteSpace(request.OrderNumber)) predicate = predicate.And(x => x.OrderNumber.Contains(request.OrderNumber));
        if (request.CustomerId.HasValue) predicate = predicate.And(x => x.CustomerId == request.CustomerId.Value);
        if (!string.IsNullOrWhiteSpace(request.CustomerName)) predicate = predicate.And(x => x.CustomerNameSnapshot.Contains(request.CustomerName));
        if (request.ProductId.HasValue) predicate = predicate.And(x => x.Lines.Any(l => l.ProductId == request.ProductId.Value));
        if (request.Status.HasValue) predicate = predicate.And(x => x.Status == request.Status.Value);
        if (!string.IsNullOrWhiteSpace(request.ExternalSystem)) predicate = predicate.And(x => x.ExternalSystem != null && x.ExternalSystem.Contains(request.ExternalSystem));
        if (request.OrderDateFrom.HasValue) predicate = predicate.And(x => x.OrderDate >= request.OrderDateFrom.Value);
        if (request.OrderDateTo.HasValue) predicate = predicate.And(x => x.OrderDate <= request.OrderDateTo.Value);
        if (request.DeliveryDateFrom.HasValue) predicate = predicate.And(x => x.RequestedDeliveryDate >= request.DeliveryDateFrom.Value);
        if (request.DeliveryDateTo.HasValue) predicate = predicate.And(x => x.RequestedDeliveryDate <= request.DeliveryDateTo.Value);

        var total = await repository.CountAsync(predicate, cancellationToken);
        var items = await repository.BrowseAsync(new Paginator<CustomerOrder>(predicate, request), cancellationToken);
        return new PagedCustomerOrdersResponse(items.Select(CustomerOrderMappers.Map).ToList(), total, request.PageSize);
    }
}
