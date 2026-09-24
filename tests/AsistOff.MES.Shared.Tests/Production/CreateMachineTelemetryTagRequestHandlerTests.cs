using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Create;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class CreateMachineTelemetryTagRequestHandlerTests
{
    private readonly Mock<IMachineTelemetryTagsRepository> _repository = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _tagId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public CreateMachineTelemetryTagRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(_tagId);
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _repository.Setup(r => r.GetByNodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MachineTelemetryTag?)null);
    }

    private CreateMachineTelemetryTagRequestHandler CreateSut() =>
        new(_repository.Object, _guids.Object, _clock.Object, _tenant.Object);

    private static CreateMachineTelemetryTagRequest ValidRequest() => new(
        Guid.NewGuid(),
        "ns=2;s=Line1.Oven.Temperature",
        "Oven temperature",
        TelemetryDataType.Double,
        30,
        null);

    [Fact]
    public async Task Handle_EmptyNodeId_ThrowsValidationException()
    {
        // Arrange
        var request = ValidRequest() with { NodeId = "  " };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_PollIntervalOutOfRange_ThrowsValidationException()
    {
        // Arrange
        var request = ValidRequest() with { PollIntervalSeconds = 0 };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_DuplicateMachineAndNode_ThrowsConflictException()
    {
        // Arrange
        var request = ValidRequest();
        _repository.Setup(r => r.GetByNodeAsync(request.MachineId, request.NodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MachineTelemetryTag
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                MachineId = request.MachineId,
                NodeId = request.NodeId,
                DisplayName = "existing"
            });

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsEnabledTagWithCallerTenant()
    {
        // Arrange
        MachineTelemetryTag? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<MachineTelemetryTag>(), It.IsAny<CancellationToken>()))
            .Callback<MachineTelemetryTag, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((MachineTelemetryTag e, CancellationToken _) => e);
        var request = ValidRequest();

        // Act
        var result = await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        saved.Should().NotBeNull();
        saved!.Id.Should().Be(_tagId);
        saved.TenantId.Should().Be(_tenantId);
        saved.MachineId.Should().Be(request.MachineId);
        saved.NodeId.Should().Be(request.NodeId);
        saved.IsEnabled.Should().BeTrue();
        result.Id.Should().Be(_tagId);
        result.IsEnabled.Should().BeTrue();
    }
}
