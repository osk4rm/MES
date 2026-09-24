using AsistOff.MES.Production.Domain.Entities;

namespace AsistOff.MES.Production.Application.Telemetry;

/// <summary>
/// Reads live values for the enabled telemetry tags of one machine and
/// appends them as <see cref="TelemetryReading"/> rows through the shared
/// ingestion service. The default implementation is simulator-backed so no
/// vendor OPC UA SDK is required in this slice.
/// </summary>
public interface IOpcUaReader
{
    /// <summary>
    /// Polls the enabled tags of <paramref name="connection"/>.MachineId and
    /// appends one reading per tag under <paramref name="tenantId"/>.
    /// Returns the number of readings appended.
    /// </summary>
    Task<int> PollAsync(
        OpcUaConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
