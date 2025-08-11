using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Configuration.Application.Features.MeasureUnits.Delete;

internal sealed class DeleteMeasureUnitRequestHandler(
    IMeasureUnitsRepository measureUnitsRepository,
    ILogger<DeleteMeasureUnitRequestHandler> logger)
    : IRequestHandler<DeleteMeasureUnitRequest>
{
    public async Task Handle(DeleteMeasureUnitRequest request, CancellationToken cancellationToken)
    {
        var measureUnit = await measureUnitsRepository.GetByIdAsync(request.Id, cancellationToken);

        if (measureUnit is null)
        {
            throw new NotFoundException($"MeasureUnit with ID {request.Id} not found");
        }

        try
        {
            await measureUnitsRepository.DeleteAsync(measureUnit.Id, cancellationToken);
            
            logger.LogInformation("MeasureUnit {Name} with ID {Id} deleted successfully", 
                measureUnit.Name, request.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting MeasureUnit with ID {Id}", request.Id);
            throw;
        }
    }
}
