using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Browse;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionConfirmations.Get;

internal sealed class GetProductionConfirmationRequestHandler(IProductionConfirmationsRepository repository)
    : IRequestHandler<GetProductionConfirmationRequest, ProductionConfirmationResponse>
{
    public async Task<ProductionConfirmationResponse> Handle(GetProductionConfirmationRequest request, CancellationToken cancellationToken)
    {
        var confirmation = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ProductionConfirmation", request.Id);

        return BrowseProductionConfirmationsRequestHandler.Map(confirmation);
    }
}
