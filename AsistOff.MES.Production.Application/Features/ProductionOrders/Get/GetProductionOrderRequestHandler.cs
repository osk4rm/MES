using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Get;

internal sealed class GetProductionOrderRequestHandler(
    IProductionOrdersRepository ordersRepository,
    IProductionConfirmationsRepository confirmationsRepository)
    : IRequestHandler<GetProductionOrderRequest, ProductionOrderResponse>
{
    public async Task<ProductionOrderResponse> Handle(GetProductionOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await ordersRepository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ProductionOrder", request.Id);
        var (produced, scrapped, count) = await confirmationsRepository.GetTotalsAsync(order.Id, cancellationToken);
        return ProductionOrderMappers.Map(order, produced, scrapped, count);
    }
}
