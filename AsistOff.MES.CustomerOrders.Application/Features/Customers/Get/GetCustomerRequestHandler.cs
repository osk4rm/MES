using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.CustomerOrders.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.CustomerOrders.Application.Features.Customers.Get;

internal sealed class GetCustomerRequestHandler(ICustomersRepository repository)
    : IRequestHandler<GetCustomerRequest, CustomerResponse>
{
    public async Task<CustomerResponse> Handle(GetCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Customer '{request.Id}' was not found.");
        return CustomerOrderMappers.Map(customer);
    }
}
