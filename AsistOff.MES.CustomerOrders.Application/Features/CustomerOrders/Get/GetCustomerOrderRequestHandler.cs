using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.CustomerOrders.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Get;

internal sealed class GetCustomerOrderRequestHandler(ICustomerOrdersRepository repository)
    : IRequestHandler<GetCustomerOrderRequest, CustomerOrderResponse>
{
    public async Task<CustomerOrderResponse> Handle(GetCustomerOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await repository.GetWithLinesAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Customer order '{request.Id}' was not found.");
        return CustomerOrderMappers.Map(order);
    }
}
