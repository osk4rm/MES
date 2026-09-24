using AsistOff.MES.Production.Application.Features.OpcUaConnections.Test;
using AsistOff.MES.Production.Application.Features.OpcUaConnections.Toggle;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class ToggleOpcUaConnectionRequestHandlerTests
{
    private readonly Mock<IOpcUaConnectionsRepository> _repository = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public ToggleOpcUaConnectionRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    private ToggleOpcUaConnectionRequestHandler CreateSut() =>
        new(_repository.Object, _clock.Object);

    private static OpcUaConnection Connection(bool isEnabled) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = Guid.NewGuid(),
        EndpointUrl = "opc.tcp://plc-1:4840",
        IsEnabled = isEnabled
    };

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFoundException()
    {
        // Arrange
        _repository.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OpcUaConnection?)null);

        // Act
        var act = () => CreateSut().Handle(new ToggleOpcUaConnectionRequest(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_EnabledConnection_DisablesIt()
    {
        // Arrange
        var connection = Connection(true);
        _repository.Setup(r => r.GetAsync(connection.Id, It.IsAny<CancellationToken>())).ReturnsAsync(connection);

        // Act
        var result = await CreateSut().Handle(new ToggleOpcUaConnectionRequest(connection.Id), CancellationToken.None);

        // Assert
        result.IsEnabled.Should().BeFalse();
        connection.UpdatedAt.Should().Be(_now);
    }

    [Fact]
    public async Task Handle_DisabledConnection_EnablesIt()
    {
        // Arrange
        var connection = Connection(false);
        _repository.Setup(r => r.GetAsync(connection.Id, It.IsAny<CancellationToken>())).ReturnsAsync(connection);

        // Act
        var result = await CreateSut().Handle(new ToggleOpcUaConnectionRequest(connection.Id), CancellationToken.None);

        // Assert
        result.IsEnabled.Should().BeTrue();
    }
}

public class TestOpcUaConnectionRequestHandlerTests
{
    private readonly Mock<IOpcUaConnectionsRepository> _repository = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public TestOpcUaConnectionRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    private TestOpcUaConnectionRequestHandler CreateSut() =>
        new(_repository.Object, _clock.Object);

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFoundException()
    {
        // Arrange
        _repository.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OpcUaConnection?)null);

        // Act
        var act = () => CreateSut().Handle(new TestOpcUaConnectionRequest(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_MalformedStoredUrl_ThrowsValidationException()
    {
        // Arrange
        var connection = new OpcUaConnection
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            MachineId = Guid.NewGuid(),
            EndpointUrl = "not-a-url"
        };
        _repository.Setup(r => r.GetAsync(connection.Id, It.IsAny<CancellationToken>())).ReturnsAsync(connection);

        // Act
        var act = () => CreateSut().Handle(new TestOpcUaConnectionRequest(connection.Id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Theory]
    [InlineData("opc.tcp://plc-1:4840")]
    [InlineData("opc.http://scada-gw/opcua")]
    [InlineData("http://scada-gw:8080/opcua")]
    [InlineData("https://scada-gw/opcua")]
    public async Task Handle_WellFormedUrl_ReturnsReachable(string endpointUrl)
    {
        // Arrange
        var connection = new OpcUaConnection
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            MachineId = Guid.NewGuid(),
            EndpointUrl = endpointUrl
        };
        _repository.Setup(r => r.GetAsync(connection.Id, It.IsAny<CancellationToken>())).ReturnsAsync(connection);

        // Act
        var result = await CreateSut().Handle(new TestOpcUaConnectionRequest(connection.Id), CancellationToken.None);

        // Assert
        result.Id.Should().Be(connection.Id);
        result.EndpointUrl.Should().Be(endpointUrl);
        result.Reachable.Should().BeTrue();
        result.CheckedAt.Should().Be(_now);
    }
}
