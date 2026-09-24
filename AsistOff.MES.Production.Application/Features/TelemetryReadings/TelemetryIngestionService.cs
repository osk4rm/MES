using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.TelemetryReadings.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using TelemetryReadingResponse = AsistOff.MES.Production.Application.Features.TelemetryReadings.TelemetryReadingResponse;
using TelemetryReadingRules = AsistOff.MES.Production.Application.Features.TelemetryReadings.TelemetryReadingRules;

namespace AsistOff.MES.Production.Application.Features.TelemetryReadings;

/// <summary>
/// Single entry point for appending telemetry readings, shared by the manual
/// Submit endpoint and the simulator poller. Validates tag ownership against
/// the supplied tenant, enforces the typed-value rule and persists the
/// reading with an explicit tenant id (background callers have no HTTP
/// tenant, so they must pass the iterated tenant id).
/// </summary>
public interface ITelemetryIngestionService
{
    Task<TelemetryReadingResponse> IngestAsync(
        Guid tagId,
        DateTime readAt,
        double? doubleValue,
        string? stringValue,
        TelemetryQuality quality,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

internal sealed class TelemetryIngestionService(
    ITelemetryReadingsRepository repository,
    IMachineTelemetryTagsRepository tagsRepository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider)
    : ITelemetryIngestionService
{
    public async Task<TelemetryReadingResponse> IngestAsync(
        Guid tagId,
        DateTime readAt,
        double? doubleValue,
        string? stringValue,
        TelemetryQuality quality,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ValidationException(nameof(tenantId), "Tenant is required.");

        // The global tenant query filter scopes this lookup to the ambient
        // tenant (HTTP caller tenant, or the iterated tenant inside a
        // BackgroundTenantContext scope), so an unknown id and a
        // cross-tenant id both yield 404. The explicit comparison below is
        // defense in depth: never persist a reading under a mismatched tenant.
        var tag = await tagsRepository.GetAsync(tagId, cancellationToken)
            ?? throw new NotFoundException("MachineTelemetryTag", tagId);
        if (tag.TenantId != tenantId)
            throw new NotFoundException("MachineTelemetryTag", tagId);

        var now = dateTimeProvider.UtcNow;
        if (readAt == default)
            throw new ValidationException(nameof(readAt), "Read time is required.");
        if (readAt > now)
            throw new ValidationException(nameof(readAt), "Read time cannot be in the future.");

        TelemetryReadingRules.ValidateValue(tag.DataType, doubleValue, stringValue);
        if (!Enum.IsDefined(quality))
            throw new ValidationException(nameof(quality), "Quality is not supported.");

        var entity = new TelemetryReading
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantId,
            TagId = tag.Id,
            MachineId = tag.MachineId,
            ReadAt = readAt,
            DoubleValue = doubleValue,
            StringValue = stringValue,
            Quality = quality
        };

        await repository.AddAsync(entity, cancellationToken);
        return BrowseTelemetryReadingsRequestHandler.Map(entity);
    }
}
