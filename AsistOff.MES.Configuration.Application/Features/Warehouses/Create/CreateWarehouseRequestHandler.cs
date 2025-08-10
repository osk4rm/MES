using AsistOff.MES.Configuration.Application.Features.Warehouses.Common;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Create;

internal sealed class CreateWarehouseRequestHandler(
    IWarehousesRepository warehousesRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext,
    ILogger<CreateWarehouseRequestHandler> logger)
    : IRequestHandler<CreateWarehouseRequest, WarehouseResult>
{
    public async Task<WarehouseResult> Handle(CreateWarehouseRequest request,
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
            throw;
        }
    }
}