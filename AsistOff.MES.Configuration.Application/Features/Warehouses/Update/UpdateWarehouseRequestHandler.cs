using AsistOff.MES.Configuration.Domain.Errors;
using AsistOff.MES.Configuration.Domain.Repositories;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Update;

internal sealed class UpdateWarehouseRequestHandler(
    IWarehousesRepository warehousesRepository)
    : IRequestHandler<UpdateWarehouseRequest, Unit>
{
    public async Task<Unit> Handle(UpdateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var warehouse = await warehousesRepository.GetByIdAsync(request.Id, cancellationToken);

        if (warehouse is null)
        {
            Errors.Warehouses.ThrowNotFound();
        }
        
        warehouse!.Name = request.Name;
        
        await warehousesRepository.UpdateAsync(warehouse, cancellationToken);

        return Unit.Value;
    }
}