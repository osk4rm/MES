using AsistOff.MES.Production.Application.Features.TelemetryReadings.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class BrowseTelemetryReadingsRequestHandlerTests
{
    private readonly Mock<ITelemetryReadingsRepository> _repository = new();

    private BrowseTelemetryReadingsRequestHandler CreateSut() => new(_repository.Object);

    private static TelemetryReading Reading(Guid tagId, Guid machineId, DateTime readAt) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        TagId = tagId,
        MachineId = machineId,
        ReadAt = readAt,
        DoubleValue = 1.0,
        Quality = TelemetryQuality.Good
    };

    private void SetupBrowse(IReadOnlyCollection<TelemetryReading> items, int totalCount)
    {
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<TelemetryReading>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(totalCount);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<TelemetryReading>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
    }

    [Fact]
    public async Task Handle_TagFilter_MatchesOnlyThatTag()
    {
        // Arrange
        ExpressionStarter<TelemetryReading>? captured = null;
        var tagId = Guid.NewGuid();
        var machineId = Guid.NewGuid();
        var matching = Reading(tagId, machineId, new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc));
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<TelemetryReading>>(), It.IsAny<CancellationToken>()))
            .Callback<ExpressionStarter<TelemetryReading>, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(1);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<TelemetryReading>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { matching });

        // Act
        var result = await CreateSut().Handle(
            new BrowseTelemetryReadingsRequest { TagId = tagId },
            CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        var predicate = captured!.Compile();
        predicate(matching).Should().BeTrue();
        predicate(Reading(Guid.NewGuid(), machineId, matching.ReadAt)).Should().BeFalse();
        result.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_MachineAndDateRangeFilter_MatchesOnlyInsideRange()
    {
        // Arrange
        ExpressionStarter<TelemetryReading>? captured = null;
        var tagId = Guid.NewGuid();
        var machineId = Guid.NewGuid();
        var inside = Reading(tagId, machineId, new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc));
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<TelemetryReading>>(), It.IsAny<CancellationToken>()))
            .Callback<ExpressionStarter<TelemetryReading>, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(1);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<TelemetryReading>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { inside });

        // Act
        await CreateSut().Handle(
            new BrowseTelemetryReadingsRequest
            {
                MachineId = machineId,
                ReadAtFrom = new DateTime(2026, 9, 24, 9, 0, 0, DateTimeKind.Utc),
                ReadAtTo = new DateTime(2026, 9, 24, 11, 0, 0, DateTimeKind.Utc)
            },
            CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        var predicate = captured!.Compile();
        predicate(inside).Should().BeTrue();
        predicate(Reading(tagId, Guid.NewGuid(), inside.ReadAt)).Should().BeFalse();
        predicate(Reading(tagId, machineId, new DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc))).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_LatestOnly_UsesLatestQueryPath()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        var latest = Reading(Guid.NewGuid(), machineId, new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc));
        _repository.Setup(r => r.CountLatestAsync(It.IsAny<ExpressionStarter<TelemetryReading>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _repository.Setup(r => r.BrowseLatestAsync(
                It.IsAny<ExpressionStarter<TelemetryReading>>(),
                It.IsAny<Paginator<TelemetryReading>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { latest });

        // Act
        var result = await CreateSut().Handle(
            new BrowseTelemetryReadingsRequest { MachineId = machineId, LatestOnly = true },
            CancellationToken.None);

        // Assert
        result.Items.Should().ContainSingle().Which.Id.Should().Be(latest.Id);
        result.TotalCount.Should().Be(1);
        _repository.Verify(r => r.BrowseAsync(It.IsAny<Paginator<TelemetryReading>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DefaultRequest_SortsByReadAtDescending()
    {
        // Arrange
        SetupBrowse(Array.Empty<TelemetryReading>(), 0);

        // Act
        var request = new BrowseTelemetryReadingsRequest();

        // Assert
        request.RawSort.Should().ContainSingle().Which.Should().Be("ReadAt,desc");
        (await CreateSut().Handle(request, CancellationToken.None)).Items.Should().BeEmpty();
    }
}
