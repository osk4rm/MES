using AsistOff.MES.Production.Application.Features.TelemetryReadings;
using AsistOff.MES.Production.Application.Telemetry;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Providers;

namespace AsistOff.MES.Production.Infrastructure.Telemetry;

/// <summary>
/// Default <see cref="IOpcUaReader"/> that stands in for a vendor OPC UA SDK.
/// For every enabled tag of the connection machine it appends one
/// <c>Quality=Good</c> reading (random walk within the tag data type) through
/// the shared <see cref="ITelemetryIngestionService"/>, so polled and manual
/// values obey identical rules. Tags of other machines are never touched.
/// </summary>
internal sealed class SimulatorBackedOpcUaReader(
    IMachineTelemetryTagsRepository tagsRepository,
    ITelemetryReadingsRepository readingsRepository,
    ITelemetryIngestionService ingestionService,
    IDateTimeProvider dateTimeProvider)
    : IOpcUaReader
{
    public async Task<int> PollAsync(
        OpcUaConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Tenant is required.");

        var now = dateTimeProvider.UtcNow;
        var random = new Random();
        var written = 0;

        // The global tenant filter scopes this to the ambient tenant; the
        // machine filter scopes it to this connection. Disabled tags are
        // skipped — only live signals of an enabled connection are polled.
        var tags = await tagsRepository.ListEnabledAsync(cancellationToken);

        foreach (var tag in tags)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (tag.MachineId != connection.MachineId || !tag.IsEnabled)
                continue;

            var previous = await readingsRepository.GetLatestAsync(tag.Id, cancellationToken);
            var (doubleValue, stringValue) = BuildNextValue(tag, previous, random);

            await ingestionService.IngestAsync(
                tag.Id, now, doubleValue, stringValue, TelemetryQuality.Good, tenantId, cancellationToken);
            written++;
        }

        return written;
    }

    internal static (double? DoubleValue, string? StringValue) BuildNextValue(
        MachineTelemetryTag tag, TelemetryReading? previous, Random random)
    {
        return tag.DataType switch
        {
            TelemetryDataType.Boolean => ((double?)(previous?.DoubleValue >= 0.5 ? 0.0 : 1.0), null),
            TelemetryDataType.Integer => ((double?)Math.Round((previous?.DoubleValue ?? 0.0) + random.Next(-1, 2)), null),
            TelemetryDataType.String => (null, previous?.StringValue ?? "opcua-ok"),
            _ => ((double?)(previous?.DoubleValue ?? 20.0) + (random.NextDouble() * 2.0 - 1.0), null),
        };
    }
}
