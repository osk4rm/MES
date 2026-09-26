using System.Diagnostics;
using System.Diagnostics.Metrics;
using AsistOff.MES.Shared.Abstractions.Observability;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Observability;

/// <summary>
/// Unit tests for the <c>AsistOff.MES</c> business meters (issue #253):
/// counter increments with Work Center labels, reason labels on scrap and
/// downtime, positive OEE/confirmation durations, the low-cardinality guard
/// (lot codes collapse to a bounded placeholder) and trace-context
/// compatibility for exemplars.
/// </summary>
public class MesMetersTests
{
    [Fact]
    public void RecordConfirmation_IncrementsCounterOnce_WithWorkCenterAndTenantLabels()
    {
        // Arrange
        using var capture = new MeterCapture();
        var machineId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        MesMeters.RecordConfirmation(machineId, tenantId, TimeSpan.FromMilliseconds(25));

        // Assert
        var counter = capture.ForMachine(MesMeters.ConfirmationsCounterName, machineId);
        counter.Should().ContainSingle();
        counter[0].Value.Should().Be(1);
        counter[0].Tag(MesMeters.WorkCenterLabel).Should().Be(machineId.ToString("D"));
        counter[0].Tag(MesMeters.TenantLabel).Should().Be(tenantId.ToString("D"));
    }

    [Fact]
    public void RecordConfirmation_RecordsDurationHistogram_WithSameLabels()
    {
        // Arrange
        using var capture = new MeterCapture();
        var machineId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        MesMeters.RecordConfirmation(machineId, tenantId, TimeSpan.FromMilliseconds(25));

        // Assert
        var histogram = capture.ForMachine(MesMeters.ConfirmationHistogramName, machineId);
        histogram.Should().ContainSingle();
        histogram[0].Value.Should().BeApproximately(0.025, 1e-9);
        histogram[0].Tag(MesMeters.WorkCenterLabel).Should().Be(machineId.ToString("D"));
        histogram[0].Tag(MesMeters.TenantLabel).Should().Be(tenantId.ToString("D"));
    }

    [Fact]
    public void RecordScrap_CarriesReasonLabel()
    {
        // Arrange
        using var capture = new MeterCapture();
        var machineId = Guid.NewGuid();

        // Act
        MesMeters.RecordScrap(machineId, "SCRAP-TOLERANCE-OVER", Guid.NewGuid());

        // Assert
        var counter = capture.ForMachine(MesMeters.ScrapCounterName, machineId);
        counter.Should().ContainSingle();
        counter[0].Value.Should().Be(1);
        counter[0].Tag(MesMeters.WorkCenterLabel).Should().Be(machineId.ToString("D"));
        counter[0].Tag(MesMeters.ReasonCodeLabel).Should().Be("SCRAP-TOLERANCE-OVER");
    }

    [Fact]
    public void RecordDowntime_CarriesReasonLabel()
    {
        // Arrange
        using var capture = new MeterCapture();
        var machineId = Guid.NewGuid();

        // Act
        MesMeters.RecordDowntime(machineId, "DT-MAINT-BREAKDOWN", Guid.NewGuid());

        // Assert
        var counter = capture.ForMachine(MesMeters.DowntimeCounterName, machineId);
        counter.Should().ContainSingle();
        counter[0].Value.Should().Be(1);
        counter[0].Tag(MesMeters.WorkCenterLabel).Should().Be(machineId.ToString("D"));
        counter[0].Tag(MesMeters.ReasonCodeLabel).Should().Be("DT-MAINT-BREAKDOWN");
    }

    [Fact]
    public void RecordOeeSnapshot_RecordsPositiveDuration_WithWorkCenterLabel()
    {
        // Arrange
        using var capture = new MeterCapture();
        var machineId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        MesMeters.RecordOeeSnapshot(machineId, tenantId, TimeSpan.FromMilliseconds(120));

        // Assert
        var histogram = capture.ForMachine(MesMeters.OeeSnapshotHistogramName, machineId);
        histogram.Should().ContainSingle();
        histogram[0].Value.Should().BeApproximately(0.12, 1e-9);
        histogram[0].Value.Should().BeGreaterThan(0);
        histogram[0].Tag(MesMeters.WorkCenterLabel).Should().Be(machineId.ToString("D"));
        histogram[0].Tag(MesMeters.TenantLabel).Should().Be(tenantId.ToString("D"));
    }

    [Fact]
    public void RecordConfirmation_WithActiveTrace_RecordsMeasurement()
    {
        // Arrange — an active span is what lets the exporter attach the
        // trace exemplar; recording must work (and keep the span) with one.
        using var capture = new MeterCapture();
        using var activity = new Activity("mes-test-span").Start();
        var machineId = Guid.NewGuid();

        // Act
        MesMeters.RecordScrap(machineId, "SCRAP-QA-REJECT", Guid.NewGuid());

        // Assert
        Activity.Current.Should().Be(activity);
        capture.Counters(MesMeters.ScrapCounterName)
            .Should().ContainSingle(m => m.Tag(MesMeters.WorkCenterLabel) == machineId.ToString("D"));
    }

    [Theory]
    [InlineData("DT-MAINT-BREAKDOWN")]
    [InlineData("SCRAP-TOLERANCE-OVER")]
    [InlineData("OEE-abc123")]
    [InlineData("QA_REJECT.2")]
    public void NormalizeReasonCode_ControlledVocabulary_PassesThrough(string code)
    {
        MesMeters.NormalizeReasonCode(code).Should().Be(code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeReasonCode_Missing_ResolvesToUnknown(string? code)
    {
        MesMeters.NormalizeReasonCode(code).Should().Be(MesMeters.UnknownReasonCode);
    }

    [Theory]
    [InlineData("LOT/2026/09/24-001#A")] // lot code with path separators
    [InlineData("SN 88123 445")] // serial with spaces
    [InlineData("operator:jan.kowalski@plant.local")] // operator/token material
    [InlineData("DT MAINT BREAKDOWN")] // free text with spaces
    public void NormalizeReasonCode_HighCardinalityValue_CollapsesToOther(string code)
    {
        MesMeters.NormalizeReasonCode(code).Should().Be(MesMeters.OtherReasonCode);
    }

    [Fact]
    public void NormalizeReasonCode_OverlongValue_CollapsesToOther()
    {
        MesMeters.NormalizeReasonCode(new string('A', 65)).Should().Be(MesMeters.OtherReasonCode);
    }

    [Fact]
    public void RecordScrap_HighCardinalityReason_CollapsesToBoundedPlaceholder()
    {
        // Arrange
        using var capture = new MeterCapture();
        var machineId = Guid.NewGuid();

        // Act — a raw lot code must never become a label value.
        MesMeters.RecordScrap(machineId, "LOT/2026/09/24-001#A", Guid.NewGuid());

        // Assert
        capture.ForMachine(MesMeters.ScrapCounterName, machineId)
            .Should().ContainSingle()
            .Which.Tag(MesMeters.ReasonCodeLabel).Should().Be(MesMeters.OtherReasonCode);
    }

    [Fact]
    public void RecordDowntime_NullReason_MapsToUnknown()
    {
        // Arrange
        using var capture = new MeterCapture();
        var machineId = Guid.NewGuid();

        // Act
        MesMeters.RecordDowntime(machineId, null, Guid.NewGuid());

        // Assert
        capture.ForMachine(MesMeters.DowntimeCounterName, machineId)
            .Should().ContainSingle()
            .Which.Tag(MesMeters.ReasonCodeLabel).Should().Be(MesMeters.UnknownReasonCode);
    }

    [Fact]
    public void Measurements_CarryOnlyBoundedLabelKeys()
    {
        // Arrange
        using var capture = new MeterCapture();
        var machineId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act — every meter entry point in one go.
        MesMeters.RecordConfirmation(machineId, tenantId, TimeSpan.FromMilliseconds(5));
        MesMeters.RecordScrap(machineId, "SCRAP-QA-REJECT", tenantId);
        MesMeters.RecordDowntime(machineId, "DT-MAINT-BREAKDOWN", tenantId);
        MesMeters.RecordOeeSnapshot(machineId, tenantId, TimeSpan.FromMilliseconds(9));

        // Assert — no lot, serial, operator or token label may ever appear.
        var allowed = new[] { MesMeters.WorkCenterLabel, MesMeters.ReasonCodeLabel, MesMeters.TenantLabel };
        var own = capture.Measurements
            .Where(m => m.Tag(MesMeters.WorkCenterLabel) == machineId.ToString("D"))
            .ToList();
        own.Should().HaveCount(5);
        own.Should().OnlyContain(m => m.Tags.All(t => allowed.Contains(t.Key)));
    }

    /// <summary>
    /// In-process listener over the shared <c>AsistOff.MES</c> meter. Tests
    /// use unique ids per case and filter by them, so parallel suites sharing
    /// the process cannot cross-contaminate assertions.
    /// </summary>
    private sealed class MeterCapture : IDisposable
    {
        public sealed record Captured(string Instrument, double Value, IReadOnlyList<KeyValuePair<string, object?>> Tags)
        {
            public string? Tag(string key) =>
                Tags.FirstOrDefault(t => t.Key == key).Value?.ToString();
        }

        public readonly List<Captured> Measurements = new();
        private readonly MeterListener _listener = new();

        public MeterCapture()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == MesMeters.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>(Record);
            _listener.SetMeasurementEventCallback<double>(Record);
            _listener.Start();
        }

        public IReadOnlyList<Captured> Counters(string instrument) =>
            Measurements.Where(m => m.Instrument == instrument).ToList();

        /// <summary>
        /// Measurements for one instrument emitted by a single test case.
        /// Every meter call carries <c>work_center_id</c>, and each test uses
        /// a unique machine id, so parallel suites sharing the process cannot
        /// cross-contaminate assertions.
        /// </summary>
        public IReadOnlyList<Captured> ForMachine(string instrument, Guid machineId) =>
            Measurements
                .Where(m => m.Instrument == instrument
                    && m.Tag(MesMeters.WorkCenterLabel) == machineId.ToString("D"))
                .ToList();

        public void Dispose() => _listener.Dispose();

        private void Record<T>(Instrument instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? _)
            where T : struct
        {
            lock (Measurements)
            {
                Measurements.Add(new Captured(instrument.Name, Convert.ToDouble(measurement), tags.ToArray()));
            }
        }
    }
}
