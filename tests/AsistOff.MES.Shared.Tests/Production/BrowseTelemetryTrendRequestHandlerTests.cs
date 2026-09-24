using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.TelemetryReadings;
using AsistOff.MES.Production.Application.Features.TelemetryReadings.Trend;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class BrowseTelemetryTrendRequestHandlerTests
{
    private readonly Mock<IMachineTelemetryTagsRepository> _tags = new();
    private readonly Mock<ITelemetryReadingsRepository> _readings = new();

    private BrowseTelemetryTrendRequestHandler CreateSut() => new(_tags.Object, _readings.Object);

    private static MachineTelemetryTag Tag(Guid id) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        MachineId = Guid.NewGuid(),
        NodeId = "ns=2;s=trend",
        DisplayName = "Trend sensor",
        DataType = TelemetryDataType.Double,
        IsEnabled = true
    };

    private static TelemetryReading Reading(Guid tagId, DateTime readAt, double value) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        TagId = tagId,
        MachineId = Guid.NewGuid(),
        ReadAt = readAt,
        DoubleValue = value,
        Quality = TelemetryQuality.Good
    };

    [Fact]
    public void Request_ImplementsTenantRequest_AndNotAnonymous()
    {
        // Assert - new dashboard queries must stay tenant-scoped
        typeof(ITenantRequest<IReadOnlyList<TelemetryReadingResponse>>)
            .IsAssignableFrom(typeof(BrowseTelemetryTrendRequest)).Should().BeTrue();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(BrowseTelemetryTrendRequest)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsReadingsInAscendingTimeOrder()
    {
        // Arrange
        var tagId = Guid.NewGuid();
        _tags.Setup(t => t.GetAsync(tagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Tag(tagId));
        _readings.Setup(r => r.BrowseTrendAsync(tagId, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Reading(tagId, new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc), 18.0),
                Reading(tagId, new DateTime(2026, 9, 24, 10, 1, 0, DateTimeKind.Utc), 22.5)
            });

        // Act
        var result = await CreateSut().Handle(
            new BrowseTelemetryTrendRequest { TagId = tagId, Take = 50 },
            CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Select(r => r.DoubleValue).Should().ContainInOrder(18.0, 22.5);
        result.Select(r => r.TagId).Should().AllBeEquivalentTo(tagId);
    }

    [Fact]
    public async Task Handle_ForwardsTakeCap_ToRepository()
    {
        // Arrange
        var tagId = Guid.NewGuid();
        _tags.Setup(t => t.GetAsync(tagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Tag(tagId));
        _readings.Setup(r => r.BrowseTrendAsync(tagId, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TelemetryReading>());

        // Act
        await CreateSut().Handle(
            new BrowseTelemetryTrendRequest { TagId = tagId, Take = 200 },
            CancellationToken.None);

        // Assert
        _readings.Verify(r => r.BrowseTrendAsync(tagId, 200, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(201)]
    [InlineData(1000)]
    public async Task Handle_TakeOutsideRange_ThrowsValidationException(int take)
    {
        // Arrange
        var tagId = Guid.NewGuid();

        // Act
        var act = () => CreateSut().Handle(
            new BrowseTelemetryTrendRequest { TagId = tagId, Take = take },
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _tags.Verify(t => t.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownTag_ThrowsNotFoundException()
    {
        // Arrange
        var tagId = Guid.NewGuid();
        _tags.Setup(t => t.GetAsync(tagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MachineTelemetryTag?)null);

        // Act
        var act = () => CreateSut().Handle(
            new BrowseTelemetryTrendRequest { TagId = tagId, Take = 50 },
            CancellationToken.None);

        // Assert - cross-tenant tags are invisible through the tenant
        // filter, so they surface here as unknown ids (404, never data)
        await act.Should().ThrowAsync<NotFoundException>();
        _readings.Verify(r => r.BrowseTrendAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
