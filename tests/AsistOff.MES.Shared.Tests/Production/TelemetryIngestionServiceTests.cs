using AsistOff.MES.Production.Application.Features.TelemetryReadings;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class TelemetryIngestionServiceTests
{
    private readonly Mock<ITelemetryReadingsRepository> _repository = new();
    private readonly Mock<IMachineTelemetryTagsRepository> _tags = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _tagId = Guid.NewGuid();
    private readonly Guid _machineId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public TelemetryIngestionServiceTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tags.Setup(t => t.GetAsync(_tagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MachineTelemetryTag
            {
                Id = _tagId,
                TenantId = _tenantId,
                MachineId = _machineId,
                NodeId = "ns=2;s=temp",
                DisplayName = "Temp",
                DataType = TelemetryDataType.Double
            });
    }

    private TelemetryIngestionService CreateSut() =>
        new(_repository.Object, _tags.Object, _guids.Object, _clock.Object);

    [Fact]
    public async Task IngestAsync_UnknownTag_ThrowsNotFoundException()
    {
        // Arrange
        var unknownTagId = Guid.NewGuid();

        // Act
        var act = () => CreateSut().IngestAsync(
            unknownTagId, _now.AddMinutes(-1), 21.5, null, TelemetryQuality.Good, _tenantId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task IngestAsync_TagOfAnotherTenant_ThrowsNotFoundException()
    {
        // Arrange
        var otherTenantId = Guid.NewGuid();

        // Act
        var act = () => CreateSut().IngestAsync(
            _tagId, _now.AddMinutes(-1), 21.5, null, TelemetryQuality.Good, otherTenantId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _repository.Verify(r => r.AddAsync(It.IsAny<TelemetryReading>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestAsync_FutureReadAt_ThrowsValidationException()
    {
        // Act
        var act = () => CreateSut().IngestAsync(
            _tagId, _now.AddMinutes(5), 21.5, null, TelemetryQuality.Good, _tenantId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task IngestAsync_TypeMismatchedValue_ThrowsValidationException()
    {
        // Arrange - a Double tag submitted with a text value and no numeric value

        // Act
        var act = () => CreateSut().IngestAsync(
            _tagId, _now.AddMinutes(-1), null, "hot", TelemetryQuality.Good, _tenantId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task IngestAsync_ValidRequest_PersistsReadingWithCallerTenant()
    {
        // Arrange
        TelemetryReading? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<TelemetryReading>(), It.IsAny<CancellationToken>()))
            .Callback<TelemetryReading, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((TelemetryReading e, CancellationToken _) => e);
        var readAt = _now.AddMinutes(-10);

        // Act
        var result = await CreateSut().IngestAsync(
            _tagId, readAt, 21.5, null, TelemetryQuality.Good, _tenantId);

        // Assert
        saved.Should().NotBeNull();
        saved!.TenantId.Should().Be(_tenantId);
        saved.TagId.Should().Be(_tagId);
        saved.MachineId.Should().Be(_machineId);
        saved.ReadAt.Should().Be(readAt);
        saved.DoubleValue.Should().Be(21.5);
        result.TagId.Should().Be(_tagId);
        result.DoubleValue.Should().Be(21.5);
    }
}
