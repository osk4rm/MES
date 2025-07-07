using AsistOff.MES.Configuration.Domain.Errors;
using AsistOff.MES.Configuration.Domain.Repositories;
using ErrorOr;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Update;

internal sealed class UpdateWarehouseRequestHandler(
    IWarehousesRepository warehousesRepository)
    : IRequestHandler<UpdateWarehouseRequest, ErrorOr<Unit>>
{
    public async Task<ErrorOr<Unit>> Handle(UpdateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var warehouse = await warehousesRepository.GetByIdAsync(request.Id, cancellationToken);

        if (warehouse is null)
        {
            return Errors.Warehouses.NotFoundError;
        }
        
        warehouse.Name = request.Name;
        
        await warehousesRepository.UpdateAsync(warehouse, cancellationToken);

        return Unit.Value;
    }
}