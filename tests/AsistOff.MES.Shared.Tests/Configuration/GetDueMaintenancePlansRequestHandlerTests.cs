using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Due;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class GetDueMaintenancePlansRequestHandlerTests
{
    private readonly Mock<IMaintenancePlansRepository> _repository = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly DateTime _now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    public GetDueMaintenancePlansRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    private static MaintenancePlan Plan(string code, DateTime? nextDueAt) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = $"Plan {code}",
        MachineId = Guid.NewGuid(),
        TriggerType = MaintenancePlanTriggerType.Time,
        IntervalDays = 30,
        NextDueAt = nextDueAt,
        IsActive = true
    };

    private GetDueMaintenancePlansRequestHandler Sut()
        => new(_repository.Object, _clock.Object);

    private void SetupPlans(params MaintenancePlan[] plans)
    {
        _repository
            .Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plans);
    }

    [Fact]
    public async Task Handle_MixedSchedules_ListsOverdueFirstWithCorrectFlags()
    {
        // Arrange
        var overdue = Plan("PM-OVERDUE", _now.AddDays(-6));
        var dueSoon = Plan("PM-SOON", _now.AddDays(3));
        var later = Plan("PM-LATER", _now.AddDays(40));
        var unscheduled = Plan("PM-NOSCHED", null);
        SetupPlans(later, unscheduled, dueSoon, overdue);

        // Act
        var result = await Sut().Handle(new GetDueMaintenancePlansRequest(), CancellationToken.None);

        // Assert
        var codes = result.Select(p => p.Code).ToList();
        codes.Should().Equal("PM-OVERDUE", "PM-SOON", "PM-LATER");
        result.First().IsOverdue.Should().BeTrue();
        result.Skip(1).Should().OnlyContain(p => !p.IsOverdue);
        result.First().DueInDays.Should().Be(-6);
        result.Should().OnlyContain(p => p.DueInDays.HasValue);
    }

    [Fact]
    public async Task Handle_OverdueOnly_KeepsOnlyPassedDueDates()
    {
        // Arrange
        SetupPlans(Plan("PM-OVERDUE", _now.AddDays(-1)), Plan("PM-SOON", _now.AddDays(3)));

        // Act
        var result = await Sut().Handle(
            new GetDueMaintenancePlansRequest { OverdueOnly = true }, CancellationToken.None);

        // Assert
        result.Should().ContainSingle(p => p.Code == "PM-OVERDUE");
        result.Single().IsOverdue.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DueWithinDays_KeepsOverdueAndHorizonPlans()
    {
        // Arrange
        SetupPlans(
            Plan("PM-OVERDUE", _now.AddDays(-10)),
            Plan("PM-SOON", _now.AddDays(5)),
            Plan("PM-LATER", _now.AddDays(60)));

        // Act
        var result = await Sut().Handle(
            new GetDueMaintenancePlansRequest { DueWithinDays = 7 }, CancellationToken.None);

        // Assert
        result.Select(p => p.Code).Should().BeEquivalentTo("PM-OVERDUE", "PM-SOON");
    }

    [Fact]
    public async Task Handle_NoActivePlans_ReturnsEmpty()
    {
        // Arrange
        SetupPlans();

        // Act
        var result = await Sut().Handle(new GetDueMaintenancePlansRequest(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }
}
