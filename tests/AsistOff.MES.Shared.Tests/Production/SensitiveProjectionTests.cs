using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Application.Features.OpcUaConnections.Browse;
using AsistOff.MES.Production.Application.Features.OpcUaConnections.Get;
using AsistOff.MES.Production.Application.Features.OpcUaConnections.Status;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Users.Core.Rbac;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class SensitiveProjectionPolicyTests
{
    [Fact]
    public void CanSeeSensitiveDetails_ProductionWrite_IsPrivileged()
    {
        // Act
        var result = SensitiveProjectionPolicy.CanSeeSensitiveDetails(
            [RbacDefaults.ProductionRead, RbacDefaults.ProductionWrite]);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanSeeSensitiveDetails_ReadOnlyRole_IsNotPrivileged()
    {
        // Arrange — the exact grant set of the read-only user role.
        IReadOnlyCollection<string> permissions = RbacDefaults.UserPermissions;

        // Act
        var result = SensitiveProjectionPolicy.CanSeeSensitiveDetails(permissions);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanSeeSensitiveDetails_AdminSet_IsPrivileged()
    {
        // Act
        var result = SensitiveProjectionPolicy.CanSeeSensitiveDetails(RbacDefaults.AdminPermissions);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanSeeSensitiveDetails_EmptyPermissions_FailsClosed()
    {
        // Act
        var result = SensitiveProjectionPolicy.CanSeeSensitiveDetails([]);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanSeeSensitiveDetails_UnrelatedWriteGrant_IsNotPrivileged()
    {
        // Act — a writer in another module is still read-only for production detail.
        var result = SensitiveProjectionPolicy.CanSeeSensitiveDetails(
            [RbacDefaults.ProductionRead, RbacDefaults.ConfigurationWrite]);

        // Assert
        result.Should().BeFalse();
    }
}

public class OpcUaSensitiveProjectionHandlerTests
{
    private const string RawError = "opc.tcp://plc-1:4840 failed: connection refused by 10.0.4.17";

    private readonly Mock<IOpcUaConnectionsRepository> _connections = new();
    private readonly Mock<IMachineTelemetryTagsRepository> _tags = new();
    private readonly Mock<ITelemetryReadingsRepository> _readings = new();
    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly DateTime _now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<ICurrentPermissionsAccessor> PermissionsWith(params string[] permissions)
    {
        var mock = new Mock<ICurrentPermissionsAccessor>();
        mock.SetupGet(m => m.Permissions).Returns(permissions);
        return mock;
    }

    private static OpcUaConnection Connection(Guid id) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        MachineId = Guid.NewGuid(),
        EndpointUrl = "opc.tcp://plc-1:4840",
        SecurityPolicy = OpcUaSecurityPolicy.None,
        PollIntervalSeconds = 30,
        IsEnabled = true,
        LastSeenAtUtc = new DateTime(2026, 9, 26, 11, 59, 50, DateTimeKind.Utc),
        LastError = RawError,
        CreatedAt = new DateTime(2026, 9, 26, 11, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task Browse_PrivilegedCaller_KeepsRawLastError()
    {
        // Arrange
        var id = Guid.NewGuid();
        _connections.Setup(r => r.CountAsync(It.IsAny<LinqKit.ExpressionStarter<OpcUaConnection>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _connections.Setup(r => r.BrowseAsync(It.IsAny<AsistOff.MES.Shared.Abstractions.Pagination.Paginator<OpcUaConnection>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Connection(id) });
        var sut = new BrowseOpcUaConnectionsRequestHandler(
            _connections.Object, PermissionsWith(RbacDefaults.ProductionWrite).Object);

        // Act
        var result = await sut.Handle(new BrowseOpcUaConnectionsRequest(), CancellationToken.None);

        // Assert
        result.Items.Should().ContainSingle().Which.LastError.Should().Be(RawError);
    }

    [Fact]
    public async Task Browse_ReadOnlyCaller_RedactsLastError_KeepsLivenessFields()
    {
        // Arrange
        var id = Guid.NewGuid();
        _connections.Setup(r => r.CountAsync(It.IsAny<LinqKit.ExpressionStarter<OpcUaConnection>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _connections.Setup(r => r.BrowseAsync(It.IsAny<AsistOff.MES.Shared.Abstractions.Pagination.Paginator<OpcUaConnection>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Connection(id) });
        var sut = new BrowseOpcUaConnectionsRequestHandler(
            _connections.Object, PermissionsWith(RbacDefaults.ProductionRead).Object);

        // Act
        var result = await sut.Handle(new BrowseOpcUaConnectionsRequest(), CancellationToken.None);

        // Assert — only the error text is redacted; addressing and liveness stay.
        var row = result.Items.Should().ContainSingle().Subject;
        row.LastError.Should().BeNull();
        row.Id.Should().Be(id);
        row.EndpointUrl.Should().Be("opc.tcp://plc-1:4840");
        row.IsEnabled.Should().BeTrue();
        row.LastSeenAtUtc.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Get_PrivilegedCaller_KeepsRawLastError()
    {
        // Arrange
        var id = Guid.NewGuid();
        _connections.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Connection(id));
        var sut = new GetOpcUaConnectionRequestHandler(
            _connections.Object, PermissionsWith(RbacDefaults.ProductionWrite).Object);

        // Act
        var result = await sut.Handle(new GetOpcUaConnectionRequest(id), CancellationToken.None);

        // Assert
        result.LastError.Should().Be(RawError);
    }

    [Fact]
    public async Task Get_ReadOnlyCaller_RedactsLastError()
    {
        // Arrange
        var id = Guid.NewGuid();
        _connections.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Connection(id));
        var sut = new GetOpcUaConnectionRequestHandler(
            _connections.Object, PermissionsWith(RbacDefaults.ProductionRead).Object);

        // Act
        var result = await sut.Handle(new GetOpcUaConnectionRequest(id), CancellationToken.None);

        // Assert
        result.LastError.Should().BeNull();
        result.Id.Should().Be(id);
        result.EndpointUrl.Should().Be("opc.tcp://plc-1:4840");
    }

    [Fact]
    public async Task Get_UnknownId_StillThrowsNotFound_ForReadOnlyCaller()
    {
        // Arrange
        _connections.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OpcUaConnection?)null);
        var sut = new GetOpcUaConnectionRequestHandler(
            _connections.Object, PermissionsWith(RbacDefaults.ProductionRead).Object);

        // Act
        var act = () => sut.Handle(new GetOpcUaConnectionRequest(Guid.NewGuid()), CancellationToken.None);

        // Assert — redaction never masks a missing (or cross-tenant) row as empty.
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Status_PrivilegedCaller_KeepsRawLastError()
    {
        // Arrange
        var sut = CreateStatusSut(RbacDefaults.ProductionWrite);

        // Act
        var result = await sut.Handle(new GetOpcUaConnectionStatusRequest(), CancellationToken.None);

        // Assert
        result.Connections.Should().ContainSingle().Which.LastError.Should().Be(RawError);
    }

    [Fact]
    public async Task Status_ReadOnlyCaller_RedactsLastError_KeepsLivenessAndCounts()
    {
        // Arrange
        var sut = CreateStatusSut(RbacDefaults.ProductionRead);

        // Act
        var result = await sut.Handle(new GetOpcUaConnectionStatusRequest(), CancellationToken.None);

        // Assert — error text gone, health signal and totals unchanged.
        var entry = result.Connections.Should().ContainSingle().Subject;
        entry.LastError.Should().BeNull();
        entry.IsLive.Should().BeTrue();
        entry.TotalTags.Should().Be(0);
        entry.ReportingTags.Should().Be(0);
        entry.StaleTags.Should().Be(0);
        result.TotalCount.Should().Be(1);
        result.LiveCount.Should().Be(1);
        result.StaleCount.Should().Be(0);
        result.DisabledCount.Should().Be(0);
    }

    private GetOpcUaConnectionStatusRequestHandler CreateStatusSut(params string[] permissions)
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _connections.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Connection(Guid.NewGuid()) });
        _tags.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AsistOff.MES.Production.Domain.Entities.MachineTelemetryTag>());

        return new GetOpcUaConnectionStatusRequestHandler(
            _connections.Object, _tags.Object, _readings.Object, _machines.Object,
            _clock.Object, PermissionsWith(permissions).Object);
    }
}
