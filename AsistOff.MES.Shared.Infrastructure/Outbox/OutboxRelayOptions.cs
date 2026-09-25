namespace AsistOff.MES.Shared.Infrastructure.Outbox;

/// <summary>
/// Slice 2 (#259) configuration for the transactional-outbox relay. Bound
/// from the <c>Outbox</c> configuration section.
/// </summary>
public sealed class OutboxRelayOptions
{
    public const string SectionName = "Outbox";

    /// <summary>
    /// Master switch for the background relay loop. Default <c>true</c>.
    /// <c>RelayOnceAsync</c>/<c>RelayTenantAsync</c> are explicit triggers and
    /// always run when called directly (e.g. from integration tests), even
    /// while the timer loop is disabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Relay poll cadence in seconds, 1..3600. Default 10.</summary>
    public int PollIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Maximum outbox rows dispatched per tenant per cycle, 1..500
    /// (clamped by <see cref="OutboxStager"/>). Default 100.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Total delivery attempts per row before it is parked as poison, 1..100.
    /// Default 5.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;
}
