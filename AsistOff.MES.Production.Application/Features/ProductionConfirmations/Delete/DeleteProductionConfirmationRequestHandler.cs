using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionConfirmations.Delete;

internal sealed class DeleteProductionConfirmationRequestHandler(
    IProductionConfirmationsRepository confirmationsRepository,
    IProductionOrdersRepository ordersRepository,
    ILotGenealogyEdgesRepository genealogyEdgesRepository)
    : IRequestHandler<DeleteProductionConfirmationRequest>
{
    public async Task Handle(DeleteProductionConfirmationRequest request, CancellationToken cancellationToken)
    {
        var confirmation = await confirmationsRepository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ProductionConfirmation", request.Id);

        var order = await ordersRepository.GetAsync(confirmation.ProductionOrderId, cancellationToken)
            ?? throw new NotFoundException("ProductionOrder", confirmation.ProductionOrderId);

        if (order.Status is ProductionOrderStatus.Completed or ProductionOrderStatus.Closed)
            throw new ConflictException("Confirmations of Completed or Closed orders cannot be deleted.");

        // Delete the auto-posted genealogy edges in code so the delete plus
        // re-create correction model stays consistent. Runs under the tenant
        // global query filter.
        await genealogyEdgesRepository.DeleteByConfirmationAsync(confirmation.Id, cancellationToken);

        await confirmationsRepository.DeleteAsync(confirmation.Id, cancellationToken);
    }
}
