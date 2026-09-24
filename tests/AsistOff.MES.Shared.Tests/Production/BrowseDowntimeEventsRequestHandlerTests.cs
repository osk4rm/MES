using AsistOff.MES.Production.Application.Features.DowntimeEvents.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class BrowseDowntimeEventsRequestHandlerTests
{
    private readonly Mock<IDowntimeEventsRepository> _repository = new();

    private BrowseDowntimeEventsRequestHandler CreateSut() => new(_repository.Object);

    private static DowntimeEvent Event(Guid machineId, DateTime startedAt, DateTime? endedAt, Guid? reasonCodeId = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = machineId,
        ReasonCodeId = reasonCodeId ?? Guid.NewGuid(),
        StartedAt = startedAt,
        EndedAt = endedAt,
        CreatedAt = startedAt
    };

    [Fact]
    public async Task Handle_MachineFilter_MatchesOnlyThatWorkCenter()
    {
        ExpressionStarter<DowntimeEvent>? captured = null;
        var machineId = Guid.NewGuid();
        var matching = Event(machineId, new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc), null);
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<DowntimeEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<ExpressionStarter<DowntimeEvent>, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(1);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<DowntimeEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { matching });

        var result = await CreateSut().Handle(
            new BrowseDowntimeEventsRequest { MachineId = machineId },
            CancellationToken.None);

        captured.Should().NotBeNull();
        var predicate = captured!.Compile();
        predicate(matching).Should().BeTrue();
        predicate(Event(Guid.NewGuid(), matching.StartedAt, null)).Should().BeFalse();
        result.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_StatusFilterOpen_MatchesOnlyOpenEvents()
    {
        ExpressionStarter<DowntimeEvent>? captured = null;
        var open = Event(Guid.NewGuid(), new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc), null);
        var closed = Event(Guid.NewGuid(), new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc));
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<DowntimeEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<ExpressionStarter<DowntimeEvent>, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(1);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<DowntimeEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { open });

        await CreateSut().Handle(
            new BrowseDowntimeEventsRequest { Status = DowntimeEventStatus.Open },
            CancellationToken.None);

        captured.Should().NotBeNull();
        var predicate = captured!.Compile();
        predicate(open).Should().BeTrue();
        predicate(closed).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DateRangeFilter_MatchesOnlyEventsInsideRange()
    {
        ExpressionStarter<DowntimeEvent>? captured = null;
        var inside = Event(Guid.NewGuid(), new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc), null);
        var outside = Event(Guid.NewGuid(), new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc), null);
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<DowntimeEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<ExpressionStarter<DowntimeEvent>, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(1);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<DowntimeEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { inside });

        await CreateSut().Handle(
            new BrowseDowntimeEventsRequest
            {
                StartedFrom = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                StartedTo = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc)
            },
            CancellationToken.None);

        captured.Should().NotBeNull();
        var predicate = captured!.Compile();
        predicate(inside).Should().BeTrue();
        predicate(outside).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReasonCodeFilter_MatchesOnlyThatReason()
    {
        ExpressionStarter<DowntimeEvent>? captured = null;
        var reasonCodeId = Guid.NewGuid();
        var machineId = Guid.NewGuid();
        var matching = Event(machineId, new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc), null, reasonCodeId);
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<DowntimeEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<ExpressionStarter<DowntimeEvent>, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(1);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<DowntimeEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { matching });

        var result = await CreateSut().Handle(
            new BrowseDowntimeEventsRequest { ReasonCodeId = reasonCodeId },
            CancellationToken.None);

        captured.Should().NotBeNull();
        var predicate = captured!.Compile();
        predicate(matching).Should().BeTrue();
        predicate(Event(machineId, matching.StartedAt, null, Guid.NewGuid())).Should().BeFalse();
        result.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_StatusFilterClosed_MatchesOnlyClosedEvents()
    {
        ExpressionStarter<DowntimeEvent>? captured = null;
        var open = Event(Guid.NewGuid(), new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc), null);
        var closed = Event(Guid.NewGuid(), new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc));
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<DowntimeEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<ExpressionStarter<DowntimeEvent>, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(1);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<DowntimeEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { closed });

        var result = await CreateSut().Handle(
            new BrowseDowntimeEventsRequest { Status = DowntimeEventStatus.Closed },
            CancellationToken.None);

        captured.Should().NotBeNull();
        var predicate = captured!.Compile();
        predicate(closed).Should().BeTrue();
        predicate(open).Should().BeFalse();
        result.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_Paging_ForwardsPaginationAndReturnsTotalCount()
    {
        Paginator<DowntimeEvent>? capturedPaginator = null;
        var startedAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var entity = Event(Guid.NewGuid(), startedAt, null);
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<DowntimeEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<DowntimeEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<DowntimeEvent>, CancellationToken>((p, _) => capturedPaginator = p)
            .ReturnsAsync(new[] { entity });

        var result = await CreateSut().Handle(
            new BrowseDowntimeEventsRequest { PageNumber = 2, PageSize = 2 },
            CancellationToken.None);

        capturedPaginator.Should().NotBeNull();
        capturedPaginator!.Paging.PageNumber.Should().Be(2);
        capturedPaginator.Paging.PageSize.Should().Be(2);
        result.TotalCount.Should().Be(5);
        result.TotalPages.Should().Be(3);
        result.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_MapsDurationAndStatus()
    {
        var startedAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var entity = Event(Guid.NewGuid(), startedAt, startedAt.AddMinutes(30));
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<DowntimeEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<DowntimeEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entity });

        var result = await CreateSut().Handle(new BrowseDowntimeEventsRequest(), CancellationToken.None);

        var item = result.Items.Should().ContainSingle().Subject;
        item.Status.Should().Be(DowntimeEventStatus.Closed);
        item.DurationMinutes.Should().BeApproximately(30, 0.001);
    }
}
