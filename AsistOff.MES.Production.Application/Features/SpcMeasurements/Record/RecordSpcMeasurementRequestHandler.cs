using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.SpcMeasurements.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.SpcMeasurements.Record;

internal sealed class RecordSpcMeasurementRequestHandler(
    ISpcMeasurementsRepository repository,
    ISpcCharacteristicsRepository characteristicsRepository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<RecordSpcMeasurementRequest, SpcMeasurementResponse>
{
    public async Task<SpcMeasurementResponse> Handle(RecordSpcMeasurementRequest request, CancellationToken cancellationToken)
    {
        if (request.CharacteristicId == Guid.Empty)
            throw new ValidationException(nameof(request.CharacteristicId), "Characteristic is required.");

        // The global tenant query filter scopes this lookup to the caller
        // tenant, so unknown and cross-tenant ids both yield 404.
        var characteristic = await characteristicsRepository.GetByIdAsync(request.CharacteristicId, cancellationToken)
            ?? throw new NotFoundException("SpcCharacteristic", request.CharacteristicId);

        if (!characteristic.IsActive)
            throw new ValidationException(nameof(request.CharacteristicId), "Measurements cannot be recorded against an inactive characteristic.");

        if (request.MeasuredAt == default)
            throw new ValidationException(nameof(request.MeasuredAt), "Measured at is required.");

        var measuredAt = request.MeasuredAt.ToUniversalTime();
        if (measuredAt > dateTimeProvider.UtcNow.AddMinutes(5))
            throw new ValidationException(nameof(request.MeasuredAt), "Measured at cannot be more than 5 minutes in the future.");

        if (request.Notes is { Length: > 1000 })
            throw new ValidationException(nameof(request.Notes), "Notes cannot exceed 1000 characters.");

        var measurement = new SpcMeasurement
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            CharacteristicId = characteristic.Id,
            Value = request.Value,
            MeasuredAt = measuredAt,
            Notes = request.Notes,
            CreatedAt = dateTimeProvider.UtcNow
        };

        await repository.AddAsync(measurement, cancellationToken);
        return BrowseSpcMeasurementsRequestHandler.Map(measurement);
    }
}
