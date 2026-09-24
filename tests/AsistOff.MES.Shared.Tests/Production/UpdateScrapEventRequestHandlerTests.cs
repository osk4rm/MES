using AsistOff.MES.Production.Application.Features.ScrapEvents.Update;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class UpdateScrapEventRequestHandlerTests
{
    private readonly Mock<IScrapEventsRepository> _repository = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly DateTime _reportedAt = new(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc);

    private UpdateScrapEventRequestHandler CreateSut() =>
        new(_repository.Object, _clock.Object);

    private ScrapEvent Existing() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = Guid.NewGuid(),
        ReasonCodeId = Guid.NewGuid(),
        Quantity = 5m,
        ReportedAt = _reportedAt,
        Notes = "old"
    };

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScrapEvent?)null);

        var act = () => CreateSut().Handle(
            new UpdateScrapEventRequest(Guid.NewGuid(), Guid.NewGuid(), 1m, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_InvalidQuantity_ThrowsValidationException()
    {
        _repository.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Existing());

        var act = () => CreateSut().Handle(
            new UpdateScrapEventRequest(Guid.NewGuid(), Guid.NewGuid(), 0m, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_HappyPath_UpdatesQuantityReasonCodeAndNotes_WhileReportedAtStaysUnchanged()
    {
        var existing = Existing();
        var originalMachineId = existing.MachineId;
        _repository.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var newReasonCodeId = Guid.NewGuid();
        await CreateSut().Handle(
            new UpdateScrapEventRequest(existing.Id, newReasonCodeId, 7.5m, "reclassified"),
            CancellationToken.None);

        existing.Quantity.Should().Be(7.5m);
        existing.ReasonCodeId.Should().Be(newReasonCodeId);
        existing.Notes.Should().Be("reclassified");
        existing.ReportedAt.Should().Be(_reportedAt);
        existing.MachineId.Should().Be(originalMachineId);
        _repository.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }
}
