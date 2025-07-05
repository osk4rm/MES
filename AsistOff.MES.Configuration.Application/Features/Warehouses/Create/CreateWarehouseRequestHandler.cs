using AsistOff.MES.Configuration.Application.Features.Warehouses.Common;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Errors;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Shared.Abstractions.Providers;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Create;

internal sealed class CreateWarehouseRequestHandler(
    ITenantContext tenantContext,
    IWarehousesRepository warehousesRepository,
    IGuidProvider guidProvider,
    ILogger<CreateWarehouseRequestHandler> logger)
    : IRequestHandler<CreateWarehouseRequest, ErrorOr<WarehouseResult>>
{
    public async Task<ErrorOr<WarehouseResult>> Handle(CreateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var warehouse = new Warehouse
        {
            Id = guidProvider.NewGuid(),
            Name = request.Name,
            SyncId = request.SyncId,
            TenantId = tenantContext.TenantId
        };

        try
        {
            var createdWarehouse = await warehousesRepository.AddAsync(warehouse, cancellationToken);
            return new WarehouseResult(createdWarehouse.Id, createdWarehouse.Name, createdWarehouse.SyncId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to create a warehouse with request {Request}.", request);
            return Errors.Warehouses.CannotAddWarehouse;
        }
    }
}