using AsistOff.MES.Configuration.Application.Features.Warehouses.Common;
using AsistOff.MES.Configuration.Domain.Errors;
using AsistOff.MES.Configuration.Domain.Repositories;
using ErrorOr;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Get;

public class GetWarehouseRequestHandler(IWarehousesRepository warehousesRepository)
    : IRequestHandler<GetWarehouseRequest, ErrorOr<WarehouseResult>>
{
    public async Task<ErrorOr<WarehouseResult>> Handle(GetWarehouseRequest request, CancellationToken cancellationToken)
    {
        var warehouse = await warehousesRepository.GetByIdAsync(request.WarehouseId, cancellationToken);

        if (warehouse is null)
        {
            return Errors.Warehouses.NotFoundError;
        }
        
        return new WarehouseResult(warehouse.Id, warehouse.Name, warehouse.SyncId);
    }
}