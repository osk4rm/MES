using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.OpcUaConnections.Create;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class CreateOpcUaConnectionRequestHandlerTests
{
    private readonly Mock<IOpcUaConnectionsRepository> _repository = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _connectionId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public CreateOpcUaConnectionRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(_connectionId);
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _repository.Setup(r => r.GetByEndpointAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OpcUaConnection?)null);
    }

    private CreateOpcUaConnectionRequestHandler CreateSut() =>
        new(_repository.Object, _guids.Object, _clock.Object, _tenant.Object);

    private static CreateOpcUaConnectionRequest ValidRequest() => new(
        Guid.NewGuid(),
        "opc.tcp://plc-1:4840",
        OpcUaSecurityPolicy.None,
        30);

    [Fact]
    public async Task Handle_BadScheme_ThrowsValidationException()
    {
        // Arrange
        var request = ValidRequest() with { EndpointUrl = "ftp://plc-1/data" };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_RelativeUrl_ThrowsValidationException()
    {
        // Arrange
        var request = ValidRequest() with { EndpointUrl = "not-a-url" };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EmptyMachineId_ThrowsValidationException()
    {
        // Arrange
        var request = ValidRequest() with { MachineId = Guid.Empty };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Theory]
    [InlineData(4)]
    [InlineData(3601)]
    public async Task Handle_PollIntervalOutOfRange_ThrowsValidationException(int pollIntervalSeconds)
    {
        // Arrange
        var request = ValidRequest() with { PollIntervalSeconds = pollIntervalSeconds };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_DuplicateEndpointForSameMachine_ThrowsConflictException()
    {
        // Arrange
        var request = ValidRequest();
        _repository.Setup(r => r.GetByEndpointAsync(request.MachineId, request.EndpointUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpcUaConnection
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                MachineId = request.MachineId,
                EndpointUrl = request.EndpointUrl
            });

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsEnabledConnectionWithCallerTenant()
    {
        // Arrange
        OpcUaConnection? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<OpcUaConnection>(), It.IsAny<CancellationToken>()))
            .Callback<OpcUaConnection, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((OpcUaConnection e, CancellationToken _) => e);
        var request = ValidRequest();

        // Act
        var result = await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        saved.Should().NotBeNull();
        saved!.Id.Should().Be(_connectionId);
        saved.TenantId.Should().Be(_tenantId);
        saved.MachineId.Should().Be(request.MachineId);
        saved.EndpointUrl.Should().Be(request.EndpointUrl);
        saved.IsEnabled.Should().BeTrue();
        saved.LastSeenAtUtc.Should().BeNull();
        saved.LastError.Should().BeNull();
        result.Id.Should().Be(_connectionId);
        result.IsEnabled.Should().BeTrue();
    }
}
