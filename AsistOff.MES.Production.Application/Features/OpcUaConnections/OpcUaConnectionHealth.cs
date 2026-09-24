using AsistOff.MES.Production.Domain.Entities;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections;

/// <summary>
/// Health rules for OPC UA connections and their telemetry tags, shared by
/// the status query (API) and the background poller. A connection is live
/// only when it is enabled and its last successful poll is within twice its
/// poll interval; an enabled connection that never reported (null
/// <c>LastSeenAtUtc</c>) or aged beyond the threshold is stale, while a
/// disabled connection is not applicable (never live, excluded from live
/// totals). Tag reporting uses the same 2x rule against the tag's own poll
/// interval and the latest <c>TelemetryReading</c>.
/// </summary>
public static class OpcUaConnectionHealth
{
    /// <summary>Staleness threshold for a connection: twice its poll interval.</summary>
    public static TimeSpan StaleAfter(OpcUaConnection connection)
        => TimeSpan.FromSeconds(2L * Math.Clamp(connection.PollIntervalSeconds, OpcUaConnectionRules.MinPollIntervalSeconds, OpcUaConnectionRules.MaxPollIntervalSeconds));

    /// <summary>
    /// True when the connection is enabled and reported within its staleness
    /// threshold. Disabled and never-seen connections are never live.
    /// </summary>
    public static bool IsLive(OpcUaConnection connection, DateTime now)
    {
        if (!connection.IsEnabled)
            return false;
        if (connection.LastSeenAtUtc is null)
            return false;
        return now - connection.LastSeenAtUtc.Value <= StaleAfter(connection);
    }

    /// <summary>
    /// True when the tag has a reading within twice its own poll interval.
    /// Tags that never reported are never reporting.
    /// </summary>
    public static bool IsTagReporting(MachineTelemetryTag tag, DateTime? lastReadAt, DateTime now)
    {
        if (lastReadAt is null)
            return false;
        var threshold = TimeSpan.FromSeconds(2L * Math.Clamp(tag.PollIntervalSeconds, 1, 3600));
        return now - lastReadAt.Value <= threshold;
    }
}
