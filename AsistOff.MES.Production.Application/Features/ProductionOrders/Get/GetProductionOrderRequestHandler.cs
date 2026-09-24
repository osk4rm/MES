using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Get;

internal sealed class GetProductionOrderRequestHandler(IProductionOrdersRepository repository)
    : IRequestHandler<GetProductionOrderRequest, ProductionOrderResponse>
{
    public async Task<ProductionOrderResponse> Handle(GetProductionOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ProductionOrder", request.Id);
        return ProductionOrderMappers.Map(order);
    }
}
