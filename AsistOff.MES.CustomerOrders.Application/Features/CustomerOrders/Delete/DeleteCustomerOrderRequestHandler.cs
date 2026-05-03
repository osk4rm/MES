using AsistOff.MES.CustomerOrders.Domain.Repositories;
using MediatR;

namespace AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Delete;

internal sealed class DeleteCustomerOrderRequestHandler(ICustomerOrdersRepository repository) : IRequestHandler<DeleteCustomerOrderRequest>
{
    public Task Handle(DeleteCustomerOrderRequest request, CancellationToken cancellationToken)
        => repository.DeleteAsync(request.Id, cancellationToken);
}
