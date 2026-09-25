using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Kanban;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Kanban.Loops.Create;

internal sealed class CreateKanbanLoopRequestHandler(
    IKanbanLoopsRepository repository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateKanbanLoopRequest, KanbanLoopResponse>
{
    public async Task<KanbanLoopResponse> Handle(CreateKanbanLoopRequest request, CancellationToken cancellationToken)
    {
        KanbanMappings.ValidateLoopInput(request.Code, request.CardQuantity, request.CardsInCirculation, request.Notes);

        if (await repository.CodeExistsAsync(request.Code, null, cancellationToken))
            throw new ConflictException($"Kanban loop with code '{request.Code}' already exists.");

        var loop = new KanbanLoop
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            Code = request.Code,
            ProductId = request.ProductId,
            ConsumingMachineId = request.ConsumingMachineId,
            SupplyingWarehouseId = request.SupplyingWarehouseId,
            CardQuantity = request.CardQuantity,
            CardsInCirculation = request.CardsInCirculation,
            IsActive = true,
            Notes = request.Notes,
            CreatedAt = dateTimeProvider.UtcNow
        };

        await repository.AddAsync(loop, cancellationToken);
        return KanbanMappings.Map(loop);
    }
}
