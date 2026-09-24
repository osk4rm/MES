namespace AsistOff.MES.Production.Application.Telemetry;

/// <summary>
/// Configuration for the OPC UA background poller. Bound from the
/// <c>OpcUa</c> configuration section.
/// </summary>
public sealed class OpcUaPollingOptions
{
    public const string SectionName = "OpcUa";

    /// <summary>
    /// Master switch for the poller. Default <c>true</c> so connections are
    /// polled without extra setup; the poller is a no-op until an enabled
    /// connection exists.
    /// </summary>
    public bool PollingEnabled { get; set; } = true;

    /// <summary>Poller cadence in seconds, 5..3600. Default 30.</summary>
    public int PollingIntervalSeconds { get; set; } = 30;
}
