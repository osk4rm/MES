using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Toggle;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class ToggleMachineTelemetryTagRequestHandlerTests
{
    private readonly Mock<IMachineTelemetryTagsRepository> _repository = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public ToggleMachineTelemetryTagRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    private ToggleMachineTelemetryTagRequestHandler CreateSut() =>
        new(_repository.Object, _clock.Object);

    private static MachineTelemetryTag Tag(bool isEnabled) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = Guid.NewGuid(),
        NodeId = "ns=2;s=x",
        DisplayName = "X",
        DataType = TelemetryDataType.Double,
        IsEnabled = isEnabled
    };

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFoundException()
    {
        // Arrange
        _repository.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MachineTelemetryTag?)null);

        // Act
        var act = () => CreateSut().Handle(new ToggleMachineTelemetryTagRequest(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_EnabledTag_DisablesIt()
    {
        // Arrange
        var tag = Tag(true);
        _repository.Setup(r => r.GetAsync(tag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tag);

        // Act
        var result = await CreateSut().Handle(new ToggleMachineTelemetryTagRequest(tag.Id), CancellationToken.None);

        // Assert
        result.IsEnabled.Should().BeFalse();
        tag.UpdatedAt.Should().Be(_now);
    }

    [Fact]
    public async Task Handle_DisabledTag_EnablesIt()
    {
        // Arrange
        var tag = Tag(false);
        _repository.Setup(r => r.GetAsync(tag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tag);

        // Act
        var result = await CreateSut().Handle(new ToggleMachineTelemetryTagRequest(tag.Id), CancellationToken.None);

        // Assert
        result.IsEnabled.Should().BeTrue();
    }
}
