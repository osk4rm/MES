using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.ShiftHandovers.Create;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class CreateShiftHandoverRequestHandlerTests
{
    private static readonly DateTime From = new(2026, 9, 21, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 9, 21, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 9, 21, 14, 5, 0, DateTimeKind.Utc);

    private readonly Mock<IShiftHandoversRepository> _handovers = new();
    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IWorkCenterCalendarsRepository> _calendars = new();
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IAndonSignalsRepository> _signals = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<ICurrentUserAccessor> _user = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _handoverId = Guid.NewGuid();
    private readonly Guid _machineId = Guid.NewGuid();
    private readonly Guid _shiftId = Guid.NewGuid();

    public CreateShiftHandoverRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(_handoverId);
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _user.SetupGet(u => u.UserId).Returns(_userId);

        _machines.Setup(m => m.GetByIdAsync(_machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Machine { Id = _machineId, Code = "WC-1", Name = "Work Center 1", TenantId = _tenantId });
        _handovers.Setup(h => h.ExistsAsync(_machineId, From, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _calendars.Setup(c => c.GetByMachineIdAsync(_machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CalendarCovering(From, _shiftId));
    }

    private CreateShiftHandoverRequestHandler CreateSut() => new(
        _handovers.Object,
        _machines.Object,
        _calendars.Object,
        _orders.Object,
        _signals.Object,
        _guids.Object,
        _clock.Object,
        _tenant.Object,
        _user.Object);

    private static WorkCenterCalendar CalendarCovering(DateTime instantUtc, Guid? shiftId)
    {
        var entry = new WorkCenterCalendarEntry
        {
            Id = Guid.NewGuid(),
            DayOfWeek = instantUtc.DayOfWeek,
            StartTime = TimeOnly.FromDateTime(instantUtc.AddHours(-4)),
            EndTime = TimeOnly.FromDateTime(instantUtc.AddHours(4)),
            ShiftId = shiftId,
            IsWorking = true
        };
        return new WorkCenterCalendar
        {
            Id = Guid.NewGuid(),
            MachineId = Guid.NewGuid(),
            Entries = [entry]
        };
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsEntryWithShiftAndSnapshotCounts()
    {
        // Arrange
        _orders.Setup(o => o.CountAsync(It.IsAny<LinqKit.ExpressionStarter<ProductionOrder>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        _signals.Setup(s => s.CountAsync(It.IsAny<LinqKit.ExpressionStarter<AndonSignal>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        ShiftHandover? saved = null;
        _handovers.Setup(h => h.AddAsync(It.IsAny<ShiftHandover>(), It.IsAny<CancellationToken>()))
            .Callback<ShiftHandover, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((ShiftHandover e, CancellationToken _) => e);

        // Act
        var result = await CreateSut().Handle(
            new CreateShiftHandoverRequest(_machineId, From, To, "Watch vibration on spindle."),
            CancellationToken.None);

        // Assert
        saved.Should().NotBeNull();
        saved!.Id.Should().Be(_handoverId);
        saved.TenantId.Should().Be(_tenantId);
        saved.MachineId.Should().Be(_machineId);
        saved.ShiftId.Should().Be(_shiftId);
        saved.From.Should().Be(From);
        saved.To.Should().Be(To);
        saved.Notes.Should().Be("Watch vibration on spindle.");
        saved.OpenOrdersCount.Should().Be(3);
        saved.ActiveAndonCount.Should().Be(1);
        saved.CreatedAt.Should().Be(Now);
        saved.CreatedBy.Should().Be(_userId);

        result.Id.Should().Be(_handoverId);
        result.MachineId.Should().Be(_machineId);
        result.ShiftId.Should().Be(_shiftId);
        result.UncoveredShift.Should().BeFalse();
        result.OpenOrdersCount.Should().Be(3);
        result.ActiveAndonCount.Should().Be(1);
        result.CreatedByUserId.Should().Be(_userId);
        result.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_MachineWithoutCalendar_PersistsWithNullShiftAndUncoveredFlag()
    {
        // Arrange
        _calendars.Setup(c => c.GetByMachineIdAsync(_machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCenterCalendar?)null);
        ShiftHandover? saved = null;
        _handovers.Setup(h => h.AddAsync(It.IsAny<ShiftHandover>(), It.IsAny<CancellationToken>()))
            .Callback<ShiftHandover, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((ShiftHandover e, CancellationToken _) => e);

        // Act
        var result = await CreateSut().Handle(
            new CreateShiftHandoverRequest(_machineId, From, To, "Night shift, no calendar."),
            CancellationToken.None);

        // Assert
        saved!.ShiftId.Should().BeNull();
        result.ShiftId.Should().BeNull();
        result.UncoveredShift.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DuplicateBoundary_ThrowsConflictException()
    {
        // Arrange
        _handovers.Setup(h => h.ExistsAsync(_machineId, From, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var act = () => CreateSut().Handle(
            new CreateShiftHandoverRequest(_machineId, From, To, "Second note for the same boundary."),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        _handovers.Verify(
            h => h.AddAsync(It.IsAny<ShiftHandover>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownMachine_ThrowsNotFoundException()
    {
        // Arrange
        var unknown = Guid.NewGuid();
        _machines.Setup(m => m.GetByIdAsync(unknown, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Machine?)null);

        // Act
        var act = () => CreateSut().Handle(
            new CreateShiftHandoverRequest(unknown, From, To, "Notes."),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_EmptyNotes_ThrowsValidationException()
    {
        // Act
        var act = () => CreateSut().Handle(
            new CreateShiftHandoverRequest(_machineId, From, To, "  "),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_ReversedWindow_ThrowsValidationException()
    {
        // Act
        var act = () => CreateSut().Handle(
            new CreateShiftHandoverRequest(_machineId, To, From, "Notes."),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WindowOver24Hours_ThrowsValidationException()
    {
        // Act
        var act = () => CreateSut().Handle(
            new CreateShiftHandoverRequest(_machineId, From, From.AddHours(25), "Notes."),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_NotesOver2000Chars_ThrowsValidationException()
    {
        // Act
        var act = () => CreateSut().Handle(
            new CreateShiftHandoverRequest(_machineId, From, To, new string('n', 2001)),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
