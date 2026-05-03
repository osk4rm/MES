using AsistOff.MES.CustomerOrders.Domain.Repositories;
using MediatR;

namespace AsistOff.MES.CustomerOrders.Application.Features.Customers.Delete;

internal sealed class DeleteCustomerRequestHandler(ICustomersRepository repository) : IRequestHandler<DeleteCustomerRequest>
{
    public Task Handle(DeleteCustomerRequest request, CancellationToken cancellationToken)
        => repository.DeleteAsync(request.Id, cancellationToken);
}
