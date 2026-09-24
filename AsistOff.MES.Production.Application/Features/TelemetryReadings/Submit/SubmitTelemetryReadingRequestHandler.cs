using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.TelemetryReadings.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.TelemetryReadings.Submit;

internal sealed class SubmitTelemetryReadingRequestHandler(
    ITelemetryReadingsRepository repository,
    IMachineTelemetryTagsRepository tagsRepository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<SubmitTelemetryReadingRequest, TelemetryReadingResponse>
{
    public async Task<TelemetryReadingResponse> Handle(SubmitTelemetryReadingRequest request, CancellationToken cancellationToken)
    {
        // The global tenant query filter scopes this lookup to the caller
        // tenant, so an unknown id and a cross-tenant id both yield 404.
        var tag = await tagsRepository.GetAsync(request.TagId, cancellationToken)
            ?? throw new NotFoundException("MachineTelemetryTag", request.TagId);

        var now = dateTimeProvider.UtcNow;
        if (request.ReadAt == default)
            throw new ValidationException(nameof(request.ReadAt), "Read time is required.");
        if (request.ReadAt > now)
            throw new ValidationException(nameof(request.ReadAt), "Read time cannot be in the future.");

        TelemetryReadingRules.ValidateValue(tag.DataType, request.DoubleValue, request.StringValue);
        if (!Enum.IsDefined(request.Quality))
            throw new ValidationException(nameof(request.Quality), "Quality is not supported.");

        var entity = new TelemetryReading
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            TagId = tag.Id,
            MachineId = tag.MachineId,
            ReadAt = request.ReadAt,
            DoubleValue = request.DoubleValue,
            StringValue = request.StringValue,
            Quality = request.Quality
        };

        await repository.AddAsync(entity, cancellationToken);
        return BrowseTelemetryReadingsRequestHandler.Map(entity);
    }
}
