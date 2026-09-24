using AsistOff.MES.Production.Application.Features.ScrapEvents.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class BrowseScrapEventsRequestHandlerTests
{
    private readonly Mock<IScrapEventsRepository> _repository = new();

    private BrowseScrapEventsRequestHandler CreateSut() => new(_repository.Object);

    private Func<ScrapEvent, bool> CaptureFilter()
    {
        Paginator<ScrapEvent>? captured = null;

        _repository
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<ScrapEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.BrowseAsync(It.IsAny<Paginator<ScrapEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<ScrapEvent>, CancellationToken>((paginator, _) => captured = paginator)
            .ReturnsAsync(Array.Empty<ScrapEvent>());

        return _ =>
        {
            captured.Should().NotBeNull();
            return captured!.Filter.Compile()(_);
        };
    }

    private static ScrapEvent Event(Guid machineId, Guid reasonCodeId, DateTime reportedAt) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = machineId,
        ReasonCodeId = reasonCodeId,
        Quantity = 3m,
        ReportedAt = reportedAt
    };

    [Fact]
    public async Task Handle_MachineIdFilter_OnlyMatchesRequestedMachine()
    {
        var matches = CaptureFilter();
        var machineId = Guid.NewGuid();
        var request = new BrowseScrapEventsRequest { MachineId = machineId };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(Event(machineId, Guid.NewGuid(), DateTime.UtcNow)).Should().BeTrue();
        matches(Event(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReasonCodeIdFilter_OnlyMatchesRequestedReasonCode()
    {
        var matches = CaptureFilter();
        var reasonCodeId = Guid.NewGuid();
        var request = new BrowseScrapEventsRequest { ReasonCodeId = reasonCodeId };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(Event(Guid.NewGuid(), reasonCodeId, DateTime.UtcNow)).Should().BeTrue();
        matches(Event(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DateRangeFilter_OnlyMatchesEventsInsideRange()
    {
        var matches = CaptureFilter();
        var request = new BrowseScrapEventsRequest
        {
            ReportedFrom = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc),
            ReportedTo = new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc)
        };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(Event(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc)))
            .Should().BeTrue();
        matches(Event(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc)))
            .Should().BeFalse();
        matches(Event(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc)))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoFilters_MatchesEveryScrapEvent()
    {
        var matches = CaptureFilter();
        var request = new BrowseScrapEventsRequest();

        await CreateSut().Handle(request, CancellationToken.None);

        matches(Event(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow)).Should().BeTrue();
    }
}
