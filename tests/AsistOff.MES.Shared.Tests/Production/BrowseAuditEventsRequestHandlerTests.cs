using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.AuditEvents;
using AsistOff.MES.Production.Application.Features.AuditEvents.Browse;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Users.Core.Rbac;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class BrowseAuditEventsRequestHandlerTests
{
    private readonly Mock<IAuditEventsRepository> _repository = new();

    private static Mock<ICurrentPermissionsAccessor> PermissionsWith(params string[] permissions)
    {
        var mock = new Mock<ICurrentPermissionsAccessor>();
        mock.SetupGet(m => m.Permissions).Returns(permissions);
        return mock;
    }

    private BrowseAuditEventsRequestHandler CreateSut(params string[] permissions) =>
        new(_repository.Object, PermissionsWith(
            permissions.Length == 0 ? [RbacDefaults.ProductionWrite] : permissions).Object);

    private static AuditEventResponse Row(string entityName, Guid entityId) => new(
        Guid.NewGuid(), entityName, entityId, 1, DateTime.UtcNow, Guid.NewGuid(), "{}");

    [Fact]
    public async Task Handle_ForwardsEntityFilter_AndReturnsPagedResponse()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var rows = new List<AuditEventResponse> { Row("ProductionOrder", entityId) };
        _repository.Setup(r => r.CountAsync("ProductionOrder", entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _repository.Setup(r => r.BrowseAsync("ProductionOrder", entityId, 0, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        // Act
        var result = await CreateSut().Handle(
            new BrowseAuditEventsRequest { EntityName = "ProductionOrder", EntityId = entityId },
            CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Items.Should().BeEquivalentTo(rows);
    }

    [Fact]
    public async Task Handle_SecondPage_SkipsFirstPage()
    {
        // Arrange
        _repository.Setup(r => r.CountAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(25);
        _repository.Setup(r => r.BrowseAsync(null, null, 10, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditEventResponse>());

        // Act
        var result = await CreateSut().Handle(
            new BrowseAuditEventsRequest { PageNumber = 2, PageSize = 10 },
            CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(25);
        result.TotalPages.Should().Be(3);
        _repository.Verify(r => r.BrowseAsync(null, null, 10, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnfilteredBrowse_PassesNullFilters()
    {
        // Arrange
        _repository.Setup(r => r.CountAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository.Setup(r => r.BrowseAsync(null, null, 0, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditEventResponse>());

        // Act
        var result = await CreateSut().Handle(new BrowseAuditEventsRequest(), CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PrivilegedCaller_KeepsFullPayload()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        _repository.Setup(r => r.CountAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _repository.Setup(r => r.BrowseAsync(null, null, 0, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditEventResponse> { Row("ProductionOrder", entityId) });

        // Act
        var result = await CreateSut(RbacDefaults.ProductionWrite)
            .Handle(new BrowseAuditEventsRequest(), CancellationToken.None);

        // Assert — privileged callers keep the full change payload.
        result.Items.Should().ContainSingle().Which.Payload.Should().Be("{}");
    }

    [Fact]
    public async Task Handle_ReadOnlyCaller_RedactsPayloadToNull()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        _repository.Setup(r => r.CountAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _repository.Setup(r => r.BrowseAsync(null, null, 0, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditEventResponse>
            {
                new(Guid.NewGuid(), "ProductionOrder", entityId, 1, DateTime.UtcNow, actorId, "{\"notes\":\"secret\"}")
            });

        // Act
        var result = await CreateSut(RbacDefaults.ProductionRead)
            .Handle(new BrowseAuditEventsRequest(), CancellationToken.None);

        // Assert — the event itself stays visible, only the payload is redacted.
        var row = result.Items.Should().ContainSingle().Subject;
        row.Payload.Should().BeNull();
        row.EntityName.Should().Be("ProductionOrder");
        row.EntityId.Should().Be(entityId);
        row.ActorId.Should().Be(actorId);
        result.TotalCount.Should().Be(1);
    }
}

public class BrowseAuditEventsRequestValidatorTests
{
    private readonly BrowseAuditEventsRequestValidator _validator = new();

    [Theory]
    [InlineData("ProductionOrder")]
    [InlineData("Machine")]
    [InlineData("ProductionConfirmation")]
    public void Validate_PilotEntityName_Passes(string entityName)
    {
        // Act
        var result = _validator.Validate(
            new BrowseAuditEventsRequest { EntityName = entityName, EntityId = Guid.NewGuid() });

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_UnknownEntityName_Fails()
    {
        // Act
        var result = _validator.Validate(
            new BrowseAuditEventsRequest { EntityName = "WorkCenter" });

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EntityIdWithoutEntityName_Fails()
    {
        // Act
        var result = _validator.Validate(
            new BrowseAuditEventsRequest { EntityId = Guid.NewGuid() });

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositivePageNumber_Fails(int pageNumber)
    {
        // Act
        var result = _validator.Validate(
            new BrowseAuditEventsRequest { PageNumber = pageNumber, PageSize = 10 });

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_OutOfRangePageSize_Fails(int pageSize)
    {
        // Act
        var result = _validator.Validate(
            new BrowseAuditEventsRequest { PageNumber = 1, PageSize = pageSize });

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void BrowseRequest_IsTenantScoped()
    {
        // Assert — history isolation rides on the global query filter, so the
        // request itself must be tenant-scoped (default-deny otherwise).
        typeof(ITenantRequest<PagedResponse<AuditEventResponse>>)
            .IsAssignableFrom(typeof(BrowseAuditEventsRequest))
            .Should().BeTrue();
    }
}
