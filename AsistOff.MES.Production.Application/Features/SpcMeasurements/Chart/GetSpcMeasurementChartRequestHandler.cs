using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.SpcMeasurements.Chart;

internal sealed class GetSpcMeasurementChartRequestHandler(
    ISpcCharacteristicsRepository characteristicsRepository,
    ISpcMeasurementsRepository measurementsRepository)
    : IRequestHandler<GetSpcMeasurementChartRequest, SpcMeasurementChartResponse>
{
    public async Task<SpcMeasurementChartResponse> Handle(GetSpcMeasurementChartRequest request, CancellationToken cancellationToken)
    {
        if (request.CharacteristicId == Guid.Empty)
            throw new ValidationException(nameof(request.CharacteristicId), "Characteristic is required.");

        // The global tenant query filter scopes this lookup to the caller
        // tenant, so unknown and cross-tenant ids both yield 404.
        var characteristic = await characteristicsRepository.GetByIdAsync(request.CharacteristicId, cancellationToken)
            ?? throw new NotFoundException("SpcCharacteristic", request.CharacteristicId);

        var fromUtc = request.From?.ToUniversalTime();
        var toUtc = request.To?.ToUniversalTime();

        var measurements = await measurementsRepository.ListForCharacteristicAsync(
            characteristic.Id, fromUtc, toUtc, cancellationToken);

        var points = measurements
            .Select(m => new SpcMeasurementChartPoint(
                m.Id,
                m.Value,
                m.MeasuredAt,
                SpcMeasurementRules.IsOutOfControl(m.Value, characteristic.LowerControlLimit, characteristic.UpperControlLimit),
                SpcMeasurementRules.IsOutOfSpec(m.Value, characteristic.LowerSpecLimit, characteristic.UpperSpecLimit)))
            .ToList();

        return new SpcMeasurementChartResponse(
            characteristic.Id,
            characteristic.NominalValue,
            characteristic.LowerSpecLimit,
            characteristic.UpperSpecLimit,
            characteristic.LowerControlLimit,
            characteristic.UpperControlLimit,
            points,
            points.Count,
            points.Count(p => p.IsOutOfControl),
            points.Count(p => p.IsOutOfSpec));
    }
}
