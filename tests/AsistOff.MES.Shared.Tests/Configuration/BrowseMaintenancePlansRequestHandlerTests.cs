using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Browse;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class BrowseMaintenancePlansRequestHandlerTests
{
    private readonly Mock<IMaintenancePlansRepository> _repository = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly DateTime _now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    public BrowseMaintenancePlansRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    private BrowseMaintenancePlansRequestHandler CreateSut() => new(_repository.Object, _clock.Object);

    private Func<MaintenancePlan, bool> CaptureFilter()
    {
        Paginator<MaintenancePlan>? captured = null;

        _repository
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<MaintenancePlan>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.BrowseAsync(It.IsAny<Paginator<MaintenancePlan>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<MaintenancePlan>, CancellationToken>((paginator, _) => captured = paginator)
            .ReturnsAsync(Array.Empty<MaintenancePlan>());

        return plan =>
        {
            captured.Should().NotBeNull();
            return captured!.Filter.Compile()(plan);
        };
    }

    private static MaintenancePlan Plan(Guid machineId, bool isActive, DateTime? nextDueAt) => new()
    {
        Code = "PM-1",
        Name = "Monthly greasing",
        MachineId = machineId,
        TriggerType = MaintenancePlanTriggerType.Time,
        IntervalDays = 30,
        NextDueAt = nextDueAt,
        IsActive = isActive
    };

    [Fact]
    public async Task Handle_MachineIdFilter_OnlyMatchesRequestedMachine()
    {
        var matches = CaptureFilter();
        var machineId = Guid.NewGuid();
        var request = new BrowseMaintenancePlansRequest { MachineId = machineId };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(Plan(machineId, true, DateTime.UtcNow.AddDays(1))).Should().BeTrue();
        matches(Plan(Guid.NewGuid(), true, DateTime.UtcNow.AddDays(1))).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_IsActiveFilter_OnlyMatchesRequestedState()
    {
        var matches = CaptureFilter();
        var request = new BrowseMaintenancePlansRequest { IsActive = false };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(Plan(Guid.NewGuid(), false, null)).Should().BeTrue();
        matches(Plan(Guid.NewGuid(), true, null)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DueBeforeFilter_OnlyMatchesEarlierDueDates()
    {
        var matches = CaptureFilter();
        var cutoff = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var request = new BrowseMaintenancePlansRequest { DueBefore = cutoff };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(Plan(Guid.NewGuid(), true, cutoff.AddDays(-1))).Should().BeTrue();
        matches(Plan(Guid.NewGuid(), true, cutoff.AddDays(1))).Should().BeFalse();
        matches(Plan(Guid.NewGuid(), true, null)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoFilters_MatchesEveryPlan()
    {
        var matches = CaptureFilter();
        var request = new BrowseMaintenancePlansRequest();

        await CreateSut().Handle(request, CancellationToken.None);

        matches(Plan(Guid.NewGuid(), true, DateTime.UtcNow.AddDays(5))).Should().BeTrue();
        matches(Plan(Guid.NewGuid(), false, null)).Should().BeTrue();
    }
}
