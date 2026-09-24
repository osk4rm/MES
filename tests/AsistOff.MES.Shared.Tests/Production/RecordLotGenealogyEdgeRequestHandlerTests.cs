using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.LotGenealogy.Record;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class RecordLotGenealogyEdgeRequestHandlerTests
{
    private readonly Mock<ILotGenealogyEdgesRepository> _edges = new();
    private readonly Mock<ILotsRepository> _lots = new();
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _edgeId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public RecordLotGenealogyEdgeRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(_edgeId);
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
    }

    private RecordLotGenealogyEdgeRequestHandler CreateSut() =>
        new(_edges.Object, _lots.Object, _orders.Object, _confirmations.Object,
            _guids.Object, _clock.Object, _tenant.Object);

    private static Lot NewLot() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Code = $"LOT-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
        ProductId = Guid.NewGuid(),
        MeasureUnitId = Guid.NewGuid(),
        Quantity = 100m,
        Status = LotStatus.Available,
        CreatedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc)
    };

    private static ProductionOrder NewOrder() => new()
    {
        Id = Guid.NewGuid(),
        Code = "PO-001",
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = 100m,
        Status = ProductionOrderStatus.Released,
        ReleasedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc)
    };

    private RecordLotGenealogyEdgeRequest ValidRequest(Guid? consumedLotId = null, Guid? producedLotId = null, Guid? orderId = null) => new(
        consumedLotId ?? Guid.NewGuid(),
        producedLotId ?? Guid.NewGuid(),
        orderId ?? Guid.NewGuid(),
        null,
        Guid.NewGuid(),
        null,
        5m,
        new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
        null);

    private (Lot Consumed, Lot Produced, ProductionOrder Order) SetupRepos(RecordLotGenealogyEdgeRequest request)
    {
        var consumed = NewLot();
        consumed.Id = request.ConsumedLotId;
        var produced = NewLot();
        produced.Id = request.ProducedLotId;
        var order = NewOrder();
        order.Id = request.ProductionOrderId;
        _lots.Setup(r => r.GetAsync(consumed.Id, It.IsAny<CancellationToken>())).ReturnsAsync(consumed);
        _lots.Setup(r => r.GetAsync(produced.Id, It.IsAny<CancellationToken>())).ReturnsAsync(produced);
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return (consumed, produced, order);
    }

    [Fact]
    public async Task Handle_ValidRequest_RecordsEdge()
    {
        var request = ValidRequest();
        var (consumed, produced, order) = SetupRepos(request);
        LotGenealogyEdge? saved = null;
        _edges.Setup(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()))
            .Callback<LotGenealogyEdge, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((LotGenealogyEdge e, CancellationToken _) => e);

        var result = await CreateSut().Handle(request, CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.Id.Should().Be(_edgeId);
        saved.TenantId.Should().Be(_tenantId);
        saved.ConsumedLotId.Should().Be(consumed.Id);
        saved.ProducedLotId.Should().Be(produced.Id);
        saved.ProductionOrderId.Should().Be(order.Id);
        saved.ProductionConfirmationId.Should().BeNull();
        saved.ConsumedQuantity.Should().Be(5m);
        result.Id.Should().Be(_edgeId);
        result.ConsumedLotId.Should().Be(consumed.Id);
        result.ProducedLotId.Should().Be(produced.Id);
    }

    [Fact]
    public async Task Handle_ConfirmationOfSameOrder_LinksConfirmation()
    {
        var order = NewOrder();
        var confirmation = new ProductionConfirmation
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ProductionOrderId = order.Id,
            MachineId = Guid.NewGuid(),
            ReportedAt = new DateTime(2026, 9, 24, 9, 0, 0, DateTimeKind.Utc),
            GoodQuantity = 10m,
            ScrapQuantity = 0m,
            CreatedAt = _now
        };
        var consumed = NewLot();
        var produced = NewLot();
        var request = ValidRequest(consumed.Id, produced.Id, order.Id)
            with { ProductionConfirmationId = confirmation.Id };
        _lots.Setup(r => r.GetAsync(consumed.Id, It.IsAny<CancellationToken>())).ReturnsAsync(consumed);
        _lots.Setup(r => r.GetAsync(produced.Id, It.IsAny<CancellationToken>())).ReturnsAsync(produced);
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _confirmations.Setup(r => r.GetAsync(confirmation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(confirmation);
        LotGenealogyEdge? saved = null;
        _edges.Setup(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()))
            .Callback<LotGenealogyEdge, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((LotGenealogyEdge e, CancellationToken _) => e);

        var result = await CreateSut().Handle(request, CancellationToken.None);

        saved!.ProductionConfirmationId.Should().Be(confirmation.Id);
        result.ProductionConfirmationId.Should().Be(confirmation.Id);
    }

    [Fact]
    public async Task Handle_SameLotSelfLink_ThrowsValidationException()
    {
        var lotId = Guid.NewGuid();
        var request = ValidRequest(lotId, lotId);

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Handle_NonPositiveQuantity_ThrowsValidationException(decimal quantity)
    {
        var request = ValidRequest() with { ConsumedQuantity = quantity };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_FutureOccurredAt_ThrowsValidationException()
    {
        var request = ValidRequest() with { OccurredAt = _now.AddHours(1) };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_MissingOccurredAt_ThrowsValidationException()
    {
        var request = ValidRequest() with { OccurredAt = default };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EmptyMachineId_ThrowsValidationException()
    {
        var request = ValidRequest() with { MachineId = Guid.Empty };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_OverlongNotes_ThrowsValidationException()
    {
        var request = ValidRequest() with { Notes = new string('n', 1001) };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_UnknownConsumedLot_ThrowsNotFoundException()
    {
        var request = ValidRequest();
        _lots.Setup(r => r.GetAsync(request.ConsumedLotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lot?)null);
        _lots.Setup(r => r.GetAsync(request.ProducedLotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewLot());

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_UnknownProducedLot_ThrowsNotFoundException()
    {
        var request = ValidRequest();
        _lots.Setup(r => r.GetAsync(request.ConsumedLotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewLot());
        _lots.Setup(r => r.GetAsync(request.ProducedLotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lot?)null);

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CrossTenantLot_ResolvesToNotFound()
    {
        // Cross-tenant rows are hidden by the global query filter, so the
        // repository returns null and the handler maps it to 404.
        var request = ValidRequest();
        _lots.Setup(r => r.GetAsync(request.ConsumedLotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lot?)null);

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownOrder_ThrowsNotFoundException()
    {
        var request = ValidRequest();
        SetupRepos(request);
        _orders.Setup(r => r.GetAsync(request.ProductionOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionOrder?)null);

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_UnknownConfirmation_ThrowsNotFoundException()
    {
        var request = ValidRequest() with { ProductionConfirmationId = Guid.NewGuid() };
        SetupRepos(request);
        _confirmations.Setup(r => r.GetAsync(request.ProductionConfirmationId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionConfirmation?)null);

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ConfirmationFromAnotherOrder_ThrowsValidationException()
    {
        var request = ValidRequest() with { ProductionConfirmationId = Guid.NewGuid() };
        var (_, _, _) = SetupRepos(request);
        var foreignConfirmation = new ProductionConfirmation
        {
            Id = request.ProductionConfirmationId.Value,
            TenantId = _tenantId,
            ProductionOrderId = Guid.NewGuid(),
            MachineId = Guid.NewGuid(),
            ReportedAt = _now,
            GoodQuantity = 1m,
            CreatedAt = _now
        };
        _confirmations.Setup(r => r.GetAsync(foreignConfirmation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(foreignConfirmation);

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
