using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Kanban;
using AsistOff.MES.Production.Application.Features.Kanban.Cards.Browse;
using AsistOff.MES.Production.Application.Features.Kanban.Cards.Consume;
using AsistOff.MES.Production.Application.Features.Kanban.Cards.Order;
using AsistOff.MES.Production.Application.Features.Kanban.Cards.Replenish;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class KanbanCardTransitionHandlerTests
{
    private readonly Mock<IKanbanCardsRepository> _cards = new();
    private readonly Mock<IKanbanLoopsRepository> _loops = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
    private readonly KanbanLoop _loop;
    private readonly KanbanCard _card;

    public KanbanCardTransitionHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);

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
            CreatedAt = _now
        };

        _card = new KanbanCard
        {
            Id = Guid.NewGuid(),
            TenantId = _loop.TenantId,
            LoopId = _loop.Id,
            CardNumber = "KB-1-01",
            Status = KanbanCardStatus.Full,
            CreatedAt = _now
        };

        _cards
            .Setup(r => r.GetAsync(_card.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_card);
        _cards
            .Setup(r => r.GetAsync(It.Is<Guid>(id => id != _card.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync((KanbanCard?)null);
        _loops
            .Setup(r => r.GetAsync(_loop.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_loop);
        _loops
            .Setup(r => r.GetAsync(It.Is<Guid>(id => id != _loop.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync((KanbanLoop?)null);
        _cards
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<KanbanCard>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    private ConsumeKanbanCardRequestHandler ConsumeSut() => new(_cards.Object, _clock.Object);

    private OrderKanbanCardRequestHandler OrderSut() => new(_cards.Object, _loops.Object, _clock.Object);

    private ReplenishKanbanCardRequestHandler ReplenishSut() => new(_cards.Object, _loops.Object, _clock.Object);

    [Fact]
    public async Task Handle_FullCycle_RoundTripsThroughAllStatuses()
    {
        var consumed = await ConsumeSut().Handle(new ConsumeKanbanCardRequest(_card.Id), CancellationToken.None);

        consumed.Status.Should().Be(KanbanCardStatus.Empty);

        var ordered = await OrderSut().Handle(new OrderKanbanCardRequest(_card.Id), CancellationToken.None);

        ordered.Status.Should().Be(KanbanCardStatus.Ordered);

        var replenished = await ReplenishSut().Handle(new ReplenishKanbanCardRequest(_card.Id), CancellationToken.None);

        replenished.Status.Should().Be(KanbanCardStatus.Full);
        replenished.Id.Should().Be(_card.Id);
        _card.UpdatedAt.Should().Be(_now);
        _cards.Verify(r => r.UpdateAsync(_card, It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Theory]
    [InlineData(KanbanCardStatus.Empty)]
    [InlineData(KanbanCardStatus.Ordered)]
    public async Task Handle_ConsumeFromNonFullStatus_ThrowsConflictException(KanbanCardStatus status)
    {
        _card.Status = status;

        var act = () => ConsumeSut().Handle(new ConsumeKanbanCardRequest(_card.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _cards.Verify(r => r.UpdateAsync(It.IsAny<KanbanCard>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(KanbanCardStatus.Full)]
    [InlineData(KanbanCardStatus.Ordered)]
    public async Task Handle_OrderFromNonEmptyStatus_ThrowsConflictException(KanbanCardStatus status)
    {
        _card.Status = status;

        var act = () => OrderSut().Handle(new OrderKanbanCardRequest(_card.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _cards.Verify(r => r.UpdateAsync(It.IsAny<KanbanCard>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(KanbanCardStatus.Full)]
    [InlineData(KanbanCardStatus.Empty)]
    public async Task Handle_ReplenishFromNonOrderedStatus_ThrowsConflictException(KanbanCardStatus status)
    {
        _card.Status = status;

        var act = () => ReplenishSut().Handle(new ReplenishKanbanCardRequest(_card.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _cards.Verify(r => r.UpdateAsync(It.IsAny<KanbanCard>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ConsumeUnknownCard_ThrowsNotFoundException()
    {
        var act = () => ConsumeSut().Handle(new ConsumeKanbanCardRequest(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_OrderUnknownCard_ThrowsNotFoundException()
    {
        var act = () => OrderSut().Handle(new OrderKanbanCardRequest(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ReplenishUnknownCard_ThrowsNotFoundException()
    {
        var act = () => ReplenishSut().Handle(new ReplenishKanbanCardRequest(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CrossTenantCardId_ThrowsNotFoundException()
    {
        // The EF global query filter hides foreign-tenant rows, so the repository
        // returns null exactly like for an unknown id: no data may leak.
        _cards
            .Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((KanbanCard?)null);

        var consume = () => ConsumeSut().Handle(new ConsumeKanbanCardRequest(_card.Id), CancellationToken.None);
        var order = () => OrderSut().Handle(new OrderKanbanCardRequest(_card.Id), CancellationToken.None);
        var replenish = () => ReplenishSut().Handle(new ReplenishKanbanCardRequest(_card.Id), CancellationToken.None);

        await consume.Should().ThrowAsync<NotFoundException>();
        await order.Should().ThrowAsync<NotFoundException>();
        await replenish.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ReplenishAgainstInactiveLoop_ThrowsConflictException()
    {
        _card.Status = KanbanCardStatus.Ordered;
        _loop.IsActive = false;

        var act = () => ReplenishSut().Handle(new ReplenishKanbanCardRequest(_card.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _cards.Verify(r => r.UpdateAsync(It.IsAny<KanbanCard>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OrderBeyondCirculationLimit_ThrowsConflictException()
    {
        _card.Status = KanbanCardStatus.Empty;
        _cards
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<KanbanCard>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_loop.CardsInCirculation);

        var act = () => OrderSut().Handle(new OrderKanbanCardRequest(_card.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _cards.Verify(r => r.UpdateAsync(It.IsAny<KanbanCard>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OrderBelowCirculationLimit_Succeeds()
    {
        _card.Status = KanbanCardStatus.Empty;
        _cards
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<KanbanCard>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_loop.CardsInCirculation - 1);

        var result = await OrderSut().Handle(new OrderKanbanCardRequest(_card.Id), CancellationToken.None);

        result.Status.Should().Be(KanbanCardStatus.Ordered);
        _cards.Verify(r => r.UpdateAsync(_card, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ConsumeAndReplenish_DoNotApplyWipGuard()
    {
        var consumed = await ConsumeSut().Handle(new ConsumeKanbanCardRequest(_card.Id), CancellationToken.None);

        consumed.Status.Should().Be(KanbanCardStatus.Empty);

        _card.Status = KanbanCardStatus.Ordered;
        var replenished = await ReplenishSut().Handle(new ReplenishKanbanCardRequest(_card.Id), CancellationToken.None);

        replenished.Status.Should().Be(KanbanCardStatus.Full);
        _cards.Verify(
            r => r.CountAsync(It.IsAny<ExpressionStarter<KanbanCard>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Requests_ImplementTenantScope_AndNotAnonymous()
    {
        // NB: ITenantRequest<TResponse> does not extend the non-generic ITenantRequest
        // marker; asserting the generic form matches the codebase convention
        // (e.g. CreateKanbanCardRequest : ITenantRequest<KanbanCardResponse>).
        ((object)new ConsumeKanbanCardRequest(Guid.NewGuid())).Should()
            .BeAssignableTo<ITenantRequest<KanbanCardResponse>>();
        ((object)new OrderKanbanCardRequest(Guid.NewGuid())).Should()
            .BeAssignableTo<ITenantRequest<KanbanCardResponse>>();
        ((object)new ReplenishKanbanCardRequest(Guid.NewGuid())).Should()
            .BeAssignableTo<ITenantRequest<KanbanCardResponse>>();
        ((object)new ConsumeKanbanCardRequest(Guid.NewGuid())).Should().NotBeAssignableTo<IAllowAnonymousRequest>();
        ((object)new OrderKanbanCardRequest(Guid.NewGuid())).Should().NotBeAssignableTo<IAllowAnonymousRequest>();
        ((object)new ReplenishKanbanCardRequest(Guid.NewGuid())).Should().NotBeAssignableTo<IAllowAnonymousRequest>();
    }

    [Fact]
    public void BrowseRequest_DefaultsToCardNumberOrdering()
    {
        new BrowseKanbanCardsRequest().RawSort.Should().ContainSingle().Which.Should().Be("CardNumber");
    }

    [Fact]
    public async Task Handle_BrowseWithStatusSet_ReturnsMappedCards()
    {
        var full = _card;
        var empty = new KanbanCard
        {
            Id = Guid.NewGuid(),
            TenantId = _loop.TenantId,
            LoopId = _loop.Id,
            CardNumber = "KB-1-02",
            Status = KanbanCardStatus.Empty,
            CreatedAt = _now
        };
        _cards
            .Setup(r => r.BrowseAsync(It.IsAny<AsistOff.MES.Shared.Abstractions.Pagination.Paginator<KanbanCard>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { full, empty });
        _cards
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<KanbanCard>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        var handler = new BrowseKanbanCardsRequestHandler(_cards.Object);

        var result = await handler.Handle(
            new BrowseKanbanCardsRequest
            {
                LoopId = _loop.Id,
                Statuses = new List<KanbanCardStatus> { KanbanCardStatus.Full, KanbanCardStatus.Empty }
            },
            CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Items.Select(i => i.CardNumber).Should().Contain(["KB-1-01", "KB-1-02"]);
    }
}
