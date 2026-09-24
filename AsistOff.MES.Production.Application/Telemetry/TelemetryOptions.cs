namespace AsistOff.MES.Production.Application.Telemetry;

/// <summary>
/// Configuration for shopfloor telemetry ingestion. Bound from the
/// <c>Telemetry</c> configuration section.
/// </summary>
public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    /// <summary>
    /// Master switch for the simulator poller. Default <c>false</c> (safe in
    /// production); enabled in Development/e2e so telemetry flows without
    /// real OPC UA hardware.
    /// </summary>
    public bool SimulatorEnabled { get; set; }

    /// <summary>Simulator poll cadence in seconds, 1..3600. Default 30.</summary>
    public int SimulatorIntervalSeconds { get; set; } = 30;
}
