using AsistOff.MES.Production.Application.Features.TelemetryReadings;
using AsistOff.MES.Production.Application.Features.TelemetryReadings.Export;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class TelemetryReadingCsvFormatterTests
{
    private static TelemetryReadingResponse Response(
        double? doubleValue = 21.5,
        string? stringValue = null,
        TelemetryQuality quality = TelemetryQuality.Good) => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
            doubleValue,
            stringValue,
            quality);

    [Fact]
    public void Format_EmptyItems_ReturnsHeaderOnly()
    {
        // Act
        var csv = TelemetryReadingCsvFormatter.Format(Array.Empty<TelemetryReadingResponse>());

        // Assert
        csv.Trim().Should().Be(TelemetryReadingCsvFormatter.Header);
    }

    [Fact]
    public void Format_NumericReading_EmitsHeaderAndOneDataRow()
    {
        // Act
        var csv = TelemetryReadingCsvFormatter.Format(new[] { Response(21.5) });

        // Assert
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
        lines[0].Trim().Should().Be("Id,TagId,MachineId,ReadAt,DoubleValue,StringValue,Quality");
        lines[1].Should().Contain("21.5");
        lines[1].Should().Contain(",1");
    }

    [Fact]
    public void Format_StringValueWithCommaAndSemicolon_IsQuoted()
    {
        // Act
        var csv = TelemetryReadingCsvFormatter.Format(new[] { Response(null, "hot, humid; unstable") });

        // Assert
        csv.Should().Contain("\"hot, humid; unstable\"");
    }

    [Fact]
    public void Format_StringValueWithQuotes_DoublesQuotes()
    {
        // Act
        var csv = TelemetryReadingCsvFormatter.Format(new[] { Response(null, "say \"hi\"") });

        // Assert
        csv.Should().Contain("\"say \"\"hi\"\"\"");
    }

    [Fact]
    public void Format_NullValues_EmitEmptyColumns()
    {
        // Act
        var csv = TelemetryReadingCsvFormatter.Format(new[] { Response(null, null) });

        // Assert
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
        lines[1].Trim().Should().EndWith(",,,1");
    }
}

public class ExportTelemetryReadingsRequestHandlerTests
{
    private readonly Mock<ITelemetryReadingsRepository> _repository = new();

    private ExportTelemetryReadingsRequestHandler CreateSut() => new(_repository.Object);

    [Fact]
    public async Task Handle_BuildsPredicateFromFilters_AndCapsRowsAtMaxRows()
    {
        // Arrange
        ExpressionStarter<TelemetryReading>? captured = null;
        var tagId = Guid.NewGuid();
        var machineId = Guid.NewGuid();
        var matching = new TelemetryReading
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            TagId = tagId,
            MachineId = machineId,
            ReadAt = new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
            DoubleValue = 21.5,
            Quality = TelemetryQuality.Good
        };
        _repository.Setup(r => r.BrowseExportAsync(
                It.IsAny<ExpressionStarter<TelemetryReading>>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<ExpressionStarter<TelemetryReading>, bool, int, CancellationToken>(
                (p, _, _, _) => captured = p)
            .ReturnsAsync(new[] { matching });

        // Act
        var csv = await CreateSut().Handle(
            new ExportTelemetryReadingsRequest { TagId = tagId, MachineId = machineId },
            CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        var predicate = captured!.Compile();
        predicate(matching).Should().BeTrue();
        predicate(new TelemetryReading { TagId = Guid.NewGuid(), MachineId = machineId, ReadAt = matching.ReadAt }).Should().BeFalse();

        _repository.Verify(r => r.BrowseExportAsync(
            It.IsAny<ExpressionStarter<TelemetryReading>>(),
            false,
            TelemetryReadingCsvFormatter.MaxRows,
            It.IsAny<CancellationToken>()), Times.Once);

        csv.Should().StartWith(TelemetryReadingCsvFormatter.Header);
        csv.Should().Contain("21.5");
    }

    [Fact]
    public async Task Handle_MaxRows_Is5000()
    {
        // Assert - the issue caps CSV exports at 5000 data rows
        TelemetryReadingCsvFormatter.MaxRows.Should().Be(5000);
        await Task.CompletedTask;
    }
}
