using AsistOff.MES.Production.Application.Features.DowntimeEvents.Delete;
using AsistOff.MES.Production.Application.Features.DowntimeEvents.Update;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class UpdateDeleteDowntimeEventRequestHandlerTests
{
    private readonly Mock<IDowntimeEventsRepository> _repository = new();
    private readonly Mock<IDateTimeProvider> _clock = new();

    public UpdateDeleteDowntimeEventRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc));
    }

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
    public async Task Update_ClosedEvent_ThrowsConflictException()
    {
        var entity = OpenEvent();
        entity.EndedAt = entity.StartedAt.AddHours(1);
        _repository.Setup(r => r.GetAsync(entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var act = () => new UpdateDowntimeEventRequestHandler(_repository.Object, _clock.Object)
            .Handle(new UpdateDowntimeEventRequest(entity.Id, Guid.NewGuid(), "x"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Update_HappyPath_ChangesReasonCodeAndNotes()
    {
        var entity = OpenEvent();
        _repository.Setup(r => r.GetAsync(entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        var reasonCodeId = Guid.NewGuid();

        await new UpdateDowntimeEventRequestHandler(_repository.Object, _clock.Object)
            .Handle(new UpdateDowntimeEventRequest(entity.Id, reasonCodeId, "updated"), CancellationToken.None);

        entity.ReasonCodeId.Should().Be(reasonCodeId);
        entity.Notes.Should().Be("updated");
        _repository.Verify(r => r.UpdateAsync(entity, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_ClosedEvent_ThrowsConflictException()
    {
        var entity = OpenEvent();
        entity.EndedAt = entity.StartedAt.AddHours(1);
        _repository.Setup(r => r.GetAsync(entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var act = () => new DeleteDowntimeEventRequestHandler(_repository.Object)
            .Handle(new DeleteDowntimeEventRequest(entity.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Delete_OpenEvent_Deletes()
    {
        var entity = OpenEvent();
        _repository.Setup(r => r.GetAsync(entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        await new DeleteDowntimeEventRequestHandler(_repository.Object)
            .Handle(new DeleteDowntimeEventRequest(entity.Id), CancellationToken.None);

        _repository.Verify(r => r.DeleteAsync(entity.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
