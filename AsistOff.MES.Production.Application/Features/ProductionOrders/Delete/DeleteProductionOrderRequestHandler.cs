using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Delete;

internal sealed class DeleteProductionOrderRequestHandler(IProductionOrdersRepository repository)
    : IRequestHandler<DeleteProductionOrderRequest>
{
    public async Task Handle(DeleteProductionOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ProductionOrder", request.Id);

        if (order.Status != ProductionOrderStatus.Planned)
            throw new ConflictException("Only orders in Planned status can be deleted.");

        await repository.DeleteAsync(order.Id, cancellationToken);
    }
}
