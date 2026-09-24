using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.TelemetryReadings.Submit;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class SubmitTelemetryReadingRequestHandlerTests
{
    private readonly Mock<ITelemetryReadingsRepository> _repository = new();
    private readonly Mock<IMachineTelemetryTagsRepository> _tags = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _tagId = Guid.NewGuid();
    private readonly Guid _machineId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public SubmitTelemetryReadingRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
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

    private SubmitTelemetryReadingRequestHandler CreateSut() =>
        new(_repository.Object, _tags.Object, _guids.Object, _clock.Object, _tenant.Object);

    [Fact]
    public async Task Handle_FutureReadAt_ThrowsValidationException()
    {
        // Arrange
        var request = new SubmitTelemetryReadingRequest(_tagId, _now.AddMinutes(5), 21.5, null, TelemetryQuality.Good);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_UnknownTag_ThrowsNotFoundException()
    {
        // Arrange
        var request = new SubmitTelemetryReadingRequest(Guid.NewGuid(), _now.AddMinutes(-1), 21.5, null, TelemetryQuality.Good);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_TypeMismatchedValue_ThrowsValidationException()
    {
        // Arrange - a Double tag submitted with a text value and no numeric value
        var request = new SubmitTelemetryReadingRequest(_tagId, _now.AddMinutes(-1), null, "hot", TelemetryQuality.Good);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_NoValueAtAll_ThrowsValidationException()
    {
        // Arrange
        var request = new SubmitTelemetryReadingRequest(_tagId, _now.AddMinutes(-1), null, null, TelemetryQuality.Good);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsReadingWithCallerTenant()
    {
        // Arrange
        TelemetryReading? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<TelemetryReading>(), It.IsAny<CancellationToken>()))
            .Callback<TelemetryReading, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((TelemetryReading e, CancellationToken _) => e);
        var readAt = _now.AddMinutes(-10);
        var request = new SubmitTelemetryReadingRequest(_tagId, readAt, 21.5, null, TelemetryQuality.Good);

        // Act
        var result = await CreateSut().Handle(request, CancellationToken.None);

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
