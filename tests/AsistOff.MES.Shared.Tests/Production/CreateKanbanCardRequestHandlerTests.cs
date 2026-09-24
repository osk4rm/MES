using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Kanban.Cards.Create;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class CreateKanbanCardRequestHandlerTests
{
    private readonly Mock<IKanbanLoopsRepository> _loops = new();
    private readonly Mock<IKanbanCardsRepository> _cards = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly KanbanLoop _loop;

    public CreateKanbanCardRequestHandlerTests()
    {
        var tenantId = Guid.NewGuid();
        _tenant.SetupGet(t => t.TenantId).Returns(tenantId);
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

        _loop = new KanbanLoop
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
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
        _cards
            .Setup(r => r.CardNumberExistsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _cards
            .Setup(r => r.CountByLoopAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    private CreateKanbanCardRequestHandler CreateSut() =>
        new(_loops.Object, _cards.Object, _guids.Object, _clock.Object, _tenant.Object);

    [Fact]
    public async Task Handle_ValidRequestWithoutCardNumber_DefaultsToFullWithGeneratedNumber()
    {
        KanbanCard? persisted = null;
        _cards
            .Setup(r => r.AddAsync(It.IsAny<KanbanCard>(), It.IsAny<CancellationToken>()))
            .Callback<KanbanCard, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((KanbanCard entity, CancellationToken _) => entity);

        var result = await CreateSut().Handle(new CreateKanbanCardRequest(_loop.Id, null, null), CancellationToken.None);

        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(KanbanCardStatus.Full);
        persisted.LoopId.Should().Be(_loop.Id);
        persisted.TenantId.Should().Be(_loop.TenantId);
        persisted.CardNumber.Should().Be("KB-1-01");
        result.Status.Should().Be(KanbanCardStatus.Full);
        result.CardNumber.Should().Be("KB-1-01");
    }

    [Fact]
    public async Task Handle_UnknownLoop_ThrowsNotFoundException()
    {
        var act = () => CreateSut().Handle(new CreateKanbanCardRequest(Guid.NewGuid(), "KB-1-01", null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_DuplicateCardNumberInLoop_ThrowsConflictException()
    {
        _cards
            .Setup(r => r.CardNumberExistsAsync(_loop.Id, "KB-1-01", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => CreateSut().Handle(new CreateKanbanCardRequest(_loop.Id, "KB-1-01", null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_NotesExceedingMaxLength_ThrowsValidationException()
    {
        var act = () => CreateSut().Handle(
            new CreateKanbanCardRequest(_loop.Id, "KB-1-09", new string('N', 1001)), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
