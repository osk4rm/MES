using AsistOff.MES.Configuration.Application.Features.MeasureUnits.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Configuration.Application.Features.MeasureUnits.Create;

internal sealed class CreateMeasureUnitRequestHandler(
    IMeasureUnitsRepository measureUnitsRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext,
    ILogger<CreateMeasureUnitRequestHandler> logger)
    : IRequestHandler<CreateMeasureUnitRequest, MeasureUnitResponse>
{
    public async Task<MeasureUnitResponse> Handle(CreateMeasureUnitRequest request,
        CancellationToken cancellationToken)
    {
        var measureUnit = new MeasureUnit
        {
            Id = guidProvider.NewGuid(),
            Name = request.Name,
            Symbol = request.Symbol,
            Type = request.Type,
            ConversionFactor = request.ConversionFactor,
            BaseUnitId = request.BaseUnitId,
            IsActive = request.IsActive,
            Description = request.Description,
            SyncId = request.SyncId,
            TenantId = tenantContext.TenantId
        };

        try
        {
            var createdMeasureUnit = await measureUnitsRepository.AddAsync(measureUnit, cancellationToken);
            
            logger.LogInformation("MeasureUnit {Name} created successfully with ID {Id}", 
                request.Name, createdMeasureUnit.Id);

            return new MeasureUnitResponse(
                createdMeasureUnit.Id,
                createdMeasureUnit.Name,
                createdMeasureUnit.Symbol,
                createdMeasureUnit.Type,
                createdMeasureUnit.ConversionFactor,
                createdMeasureUnit.BaseUnitId,
                createdMeasureUnit.BaseUnit?.Name,
                createdMeasureUnit.IsActive,
                createdMeasureUnit.Description,
                createdMeasureUnit.SyncId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating MeasureUnit {Name}", request.Name);
            throw;
        }
    }
}
