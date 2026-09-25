using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Delete;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class DeleteProductionConfirmationRequestHandlerTests
{
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<ILotGenealogyEdgesRepository> _edges = new();

    private DeleteProductionConfirmationRequestHandler CreateSut() =>
        new(_confirmations.Object, _orders.Object, _edges.Object);

    private static (ProductionConfirmation Confirmation, ProductionOrder Order) Setup(ProductionOrderStatus status)
    {
        var order = new ProductionOrder
        {
            Id = Guid.NewGuid(),
            Code = "PO-001",
            ProductId = Guid.NewGuid(),
            RecipeId = Guid.NewGuid(),
            RecipeVersionId = Guid.NewGuid(),
            PlannedQuantity = 100m,
            Status = status,
            ReleasedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc)
        };
        var confirmation = new ProductionConfirmation
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ProductionOrderId = order.Id,
            MachineId = Guid.NewGuid(),
            ReportedAt = new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
            GoodQuantity = 5m,
            ScrapQuantity = 0m,
            CreatedAt = new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc)
        };
        return (confirmation, order);
    }

    private void SetupRepos(ProductionConfirmation confirmation, ProductionOrder order)
    {
        _confirmations.Setup(r => r.GetAsync(confirmation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmation);
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Released)]
    [InlineData(ProductionOrderStatus.InProgress)]
    public async Task Handle_OrderOpen_DeletesConfirmation(ProductionOrderStatus status)
    {
        var (confirmation, order) = Setup(status);
        SetupRepos(confirmation, order);

        await CreateSut().Handle(new DeleteProductionConfirmationRequest(confirmation.Id), CancellationToken.None);

        _confirmations.Verify(r => r.DeleteAsync(confirmation.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Closed)]
    public async Task Handle_OrderFinal_ThrowsConflictException(ProductionOrderStatus status)
    {
        var (confirmation, order) = Setup(status);
        SetupRepos(confirmation, order);

        var act = () => CreateSut().Handle(new DeleteProductionConfirmationRequest(confirmation.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _confirmations.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownConfirmation_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _confirmations.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionConfirmation?)null);

        var act = () => CreateSut().Handle(new DeleteProductionConfirmationRequest(id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
