using AsistOff.MES.Configuration.Application.Features.MeasureUnits.Responses;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Configuration.Application.Features.MeasureUnits.Update;

internal sealed class UpdateMeasureUnitRequestHandler(
    IMeasureUnitsRepository measureUnitsRepository,
    ILogger<UpdateMeasureUnitRequestHandler> logger)
    : IRequestHandler<UpdateMeasureUnitRequest, MeasureUnitResponse>
{
    public async Task<MeasureUnitResponse> Handle(UpdateMeasureUnitRequest request,
        CancellationToken cancellationToken)
    {
        var measureUnit = await measureUnitsRepository
            .GetByIdAsync(request.Id, cancellationToken);

        if (measureUnit is null)
            throw new NotFoundException($"MeasureUnit with ID {request.Id} not found");

        try
        {
            measureUnit.Name = request.Name;
            measureUnit.Symbol = request.Symbol;
            measureUnit.Type = request.Type;
            measureUnit.ConversionFactor = request.ConversionFactor;
            measureUnit.BaseUnitId = request.BaseUnitId;
            measureUnit.IsActive = request.IsActive;
            measureUnit.Description = request.Description;
            measureUnit.SyncId = request.SyncId;

            await measureUnitsRepository.UpdateAsync(measureUnit, cancellationToken);

            logger.LogInformation("MeasureUnit {Name} updated successfully with ID {Id}", 
                request.Name, request.Id);

            return new MeasureUnitResponse(
                measureUnit.Id,
                measureUnit.Name,
                measureUnit.Symbol,
                measureUnit.Type,
                measureUnit.ConversionFactor,
                measureUnit.BaseUnitId,
                measureUnit.BaseUnit?.Name,
                measureUnit.IsActive,
                measureUnit.Description,
                measureUnit.SyncId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating MeasureUnit {Name} with ID {Id}", request.Name, request.Id);
            throw;
        }
    }
}
