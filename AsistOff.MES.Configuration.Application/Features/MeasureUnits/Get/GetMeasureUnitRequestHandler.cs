using AsistOff.MES.Configuration.Application.Features.MeasureUnits.Responses;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Application.Features.MeasureUnits.Get;

internal sealed class GetMeasureUnitRequestHandler(
    IMeasureUnitsRepository measureUnitsRepository)
    : IRequestHandler<GetMeasureUnitRequest, MeasureUnitResponse>
{
    public async Task<MeasureUnitResponse> Handle(GetMeasureUnitRequest request,
        CancellationToken cancellationToken)
    {
        var measureUnit = await measureUnitsRepository.GetAsync(request.Id, cancellationToken);

        if (measureUnit is null)
            throw new NotFoundException($"MeasureUnit with ID {request.Id} not found");

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
}
