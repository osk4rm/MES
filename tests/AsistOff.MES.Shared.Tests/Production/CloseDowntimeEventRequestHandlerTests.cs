using AsistOff.MES.Production.Application.Features.DowntimeEvents.Close;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class CloseDowntimeEventRequestHandlerTests
{
    private readonly Mock<IDowntimeEventsRepository> _repository = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly DateTime _now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    public CloseDowntimeEventRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    private CloseDowntimeEventRequestHandler CreateSut() =>
        new(_repository.Object, _clock.Object);

    private static DowntimeEvent OpenEvent() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = Guid.NewGuid(),
        ReasonCodeId = Guid.NewGuid(),
        StartedAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
        EndedAt = null,
        CreatedAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DowntimeEvent?)null);

        var act = () => CreateSut().Handle(new CloseDowntimeEventRequest(Guid.NewGuid(), _now), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AlreadyClosedEvent_ThrowsConflictException()
    {
        var entity = OpenEvent();
        entity.EndedAt = _now;
        _repository.Setup(r => r.GetAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var act = () => CreateSut().Handle(new CloseDowntimeEventRequest(entity.Id, _now), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_EndedAtBeforeStartedAt_ThrowsValidationException()
    {
        var entity = OpenEvent();
        _repository.Setup(r => r.GetAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var act = () => CreateSut().Handle(
            new CloseDowntimeEventRequest(entity.Id, entity.StartedAt.AddMinutes(-5)),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_HappyPath_SetsEndedAtAndComputesDuration()
    {
        var entity = OpenEvent();
        _repository.Setup(r => r.GetAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var endedAt = entity.StartedAt.AddMinutes(90);
        var result = await CreateSut().Handle(new CloseDowntimeEventRequest(entity.Id, endedAt), CancellationToken.None);

        entity.EndedAt.Should().Be(endedAt);
        result.EndedAt.Should().Be(endedAt);
        result.Status.Should().Be(AsistOff.MES.Production.Domain.Enums.DowntimeEventStatus.Closed);
        result.DurationMinutes.Should().BeApproximately(90, 0.001);
        _repository.Verify(r => r.UpdateAsync(entity, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullEndedAt_UsesCurrentTime()
    {
        var entity = OpenEvent();
        _repository.Setup(r => r.GetAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await CreateSut().Handle(new CloseDowntimeEventRequest(entity.Id, null), CancellationToken.None);

        entity.EndedAt.Should().Be(_now);
        result.EndedAt.Should().Be(_now);
    }
}
