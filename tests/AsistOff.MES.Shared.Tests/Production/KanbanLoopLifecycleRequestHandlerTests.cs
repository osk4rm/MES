using AsistOff.MES.Production.Application.Features.Kanban.Cards.Delete;
using AsistOff.MES.Production.Application.Features.Kanban.Cards.Get;
using AsistOff.MES.Production.Application.Features.Kanban.Loops.Delete;
using AsistOff.MES.Production.Application.Features.Kanban.Loops.Update;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class KanbanLoopLifecycleRequestHandlerTests
{
    private readonly Mock<IKanbanLoopsRepository> _loops = new();
    private readonly Mock<IKanbanCardsRepository> _cards = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly KanbanLoop _loop;

    public KanbanLoopLifecycleRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

        _loop = new KanbanLoop
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Code = "KB-1",
            ProductId = Guid.NewGuid(),
            ConsumingMachineId = Guid.NewGuid(),
            SupplyingWarehouseId = Guid.NewGuid(),
            CardQuantity = 10m,
            CardsInCirculation = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _loops
            .Setup(r => r.GetAsync(_loop.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_loop);
        _loops
            .Setup(r => r.GetAsync(It.Is<Guid>(id => id != _loop.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync((KanbanLoop?)null);
    }

    [Fact]
    public async Task Handle_Update_AppliesMutableFieldsOnly()
    {
        var handler = new UpdateKanbanLoopRequestHandler(_loops.Object, _clock.Object);

        await handler.Handle(
            new UpdateKanbanLoopRequest(_loop.Id, 25m, 5, false, "note"), CancellationToken.None);

        _loop.CardQuantity.Should().Be(25m);
        _loop.CardsInCirculation.Should().Be(5);
        _loop.IsActive.Should().BeFalse();
        _loop.Notes.Should().Be("note");
        _loop.Code.Should().Be("KB-1");
        _loops.Verify(r => r.UpdateAsync(_loop, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UpdateWithZeroQuantity_ThrowsValidationException()
    {
        var handler = new UpdateKanbanLoopRequestHandler(_loops.Object, _clock.Object);

        var act = () => handler.Handle(
            new UpdateKanbanLoopRequest(_loop.Id, 0m, 2, true, null), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Handle_UpdateWithCirculationOutOfBounds_ThrowsValidationException(int circulation)
    {
        var handler = new UpdateKanbanLoopRequestHandler(_loops.Object, _clock.Object);

        var act = () => handler.Handle(
            new UpdateKanbanLoopRequest(_loop.Id, 10m, circulation, true, null), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_UpdateUnknownLoop_ThrowsNotFoundException()
    {
        var handler = new UpdateKanbanLoopRequestHandler(_loops.Object, _clock.Object);

        var act = () => handler.Handle(
            new UpdateKanbanLoopRequest(Guid.NewGuid(), 10m, 2, true, null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_DeleteUnknownLoop_ThrowsNotFoundException()
    {
        var handler = new DeleteKanbanLoopRequestHandler(_loops.Object);

        var act = () => handler.Handle(new DeleteKanbanLoopRequest(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_DeleteKnownLoop_Deletes()
    {
        var handler = new DeleteKanbanLoopRequestHandler(_loops.Object);

        await handler.Handle(new DeleteKanbanLoopRequest(_loop.Id), CancellationToken.None);

        _loops.Verify(r => r.DeleteAsync(_loop.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_GetUnknownCard_ThrowsNotFoundException()
    {
        _cards
            .Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((KanbanCard?)null);
        var handler = new GetKanbanCardRequestHandler(_cards.Object);

        var act = () => handler.Handle(new GetKanbanCardRequest(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_DeleteUnknownCard_ThrowsNotFoundException()
    {
        _cards
            .Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((KanbanCard?)null);
        var handler = new DeleteKanbanCardRequestHandler(_cards.Object);

        var act = () => handler.Handle(new DeleteKanbanCardRequest(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_GetKnownCard_ReturnsResponse()
    {
        var card = new KanbanCard
        {
            Id = Guid.NewGuid(),
            TenantId = _loop.TenantId,
            LoopId = _loop.Id,
            CardNumber = "KB-1-01",
            Status = KanbanCardStatus.Full,
            CreatedAt = DateTime.UtcNow
        };
        _cards
            .Setup(r => r.GetAsync(card.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(card);
        var handler = new GetKanbanCardRequestHandler(_cards.Object);

        var result = await handler.Handle(new GetKanbanCardRequest(card.Id), CancellationToken.None);

        result.CardNumber.Should().Be("KB-1-01");
        result.Status.Should().Be(KanbanCardStatus.Full);
    }
}
