using AsistOff.MES.Production.Application.Features.Lots.ChangeStatus;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class ChangeLotStatusRequestHandlerTests
{
    private readonly Mock<ILotsRepository> _repository = new();
    private readonly Mock<IDateTimeProvider> _clock = new();

    public ChangeLotStatusRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);
    }

    private ChangeLotStatusRequestHandler CreateSut() =>
        new(_repository.Object, _clock.Object);

    private void SetupLot(LotStatus status, Guid? id = null)
    {
        var lotId = id ?? Guid.NewGuid();
        _repository
            .Setup(r => r.GetAsync(lotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Lot
            {
                Id = lotId,
                Code = "LOT-1",
                ProductId = Guid.NewGuid(),
                MeasureUnitId = Guid.NewGuid(),
                Quantity = 5m,
                Status = status
            });
    }

    [Fact]
    public async Task Handle_AvailableToOnHold_AppliesTransition()
    {
        var id = Guid.NewGuid();
        SetupLot(LotStatus.Available, id);

        await CreateSut().Handle(new ChangeLotStatusRequest(id, LotStatus.OnHold), CancellationToken.None);

        _repository.Verify(r => r.UpdateAsync(
            It.Is<Lot>(l => l.Id == id && l.Status == LotStatus.OnHold),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OnHoldToAvailable_AppliesTransition()
    {
        var id = Guid.NewGuid();
        SetupLot(LotStatus.OnHold, id);

        await CreateSut().Handle(new ChangeLotStatusRequest(id, LotStatus.Available), CancellationToken.None);

        _repository.Verify(r => r.UpdateAsync(
            It.Is<Lot>(l => l.Status == LotStatus.Available),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ScrappedToAvailable_ThrowsValidationException()
    {
        var id = Guid.NewGuid();
        SetupLot(LotStatus.Scrapped, id);

        var act = () => CreateSut().Handle(new ChangeLotStatusRequest(id, LotStatus.Available), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_UnknownLot_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lot?)null);

        var act = () => CreateSut().Handle(new ChangeLotStatusRequest(id, LotStatus.OnHold), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
