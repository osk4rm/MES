using AsistOff.MES.Configuration.Application.Features.StockMovements.Browse;
using AsistOff.MES.Configuration.Application.Features.StockMovements.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class BrowseStockMovementsRequestHandlerTests
{
    private readonly Mock<IStockMovementsRepository> _movements = new();
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();

    private BrowseStockMovementsRequestHandler CreateSut() =>
        new(_movements.Object, _confirmations.Object);

    private static ProductionConfirmation Confirmation(Guid id) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        ProductionOrderId = Guid.NewGuid(),
        MachineId = Guid.NewGuid(),
        ReportedAt = DateTime.UtcNow,
        GoodQuantity = 10m,
        ScrapQuantity = 0m
    };

    private static StockMovement Line(Guid confirmationId, string type, Guid productId, decimal quantity) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MovementType = type,
        ProductId = productId,
        Quantity = quantity,
        ProductionConfirmationId = confirmationId,
        ProductionOrderId = Guid.NewGuid(),
        ReportedAt = DateTime.UtcNow
    };

    [Fact]
    public void Request_ImplementsTenantRequest()
    {
        typeof(BrowseStockMovementsRequest).Should().Implement<ITenantRequest<IReadOnlyList<StockMovementResponse>>>();
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsMappedLines()
    {
        // Arrange
        var confirmation = Confirmation(Guid.NewGuid());
        var productId = Guid.NewGuid();
        _confirmations.Setup(r => r.GetAsync(confirmation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmation);
        _movements.Setup(r => r.ListForConfirmationAsync(confirmation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Line(confirmation.Id, StockMovement.ReceiptType, productId, 10m),
                Line(confirmation.Id, StockMovement.IssueType, Guid.NewGuid(), 20m)
            ]);

        // Act
        var result = await CreateSut().Handle(
            new BrowseStockMovementsRequest { ConfirmationId = confirmation.Id }, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainSingle(x => x.MovementType == "PW" && x.ProductId == productId && x.Quantity == 10m);
        result.Should().OnlyContain(x => x.ProductionConfirmationId == confirmation.Id);
    }

    [Fact]
    public async Task Handle_UnknownConfirmation_ThrowsNotFoundException()
    {
        // Arrange
        _confirmations.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionConfirmation?)null);

        // Act
        var act = () => CreateSut().Handle(
            new BrowseStockMovementsRequest { ConfirmationId = Guid.NewGuid() }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_MissingConfirmationId_ThrowsValidationException()
    {
        // Act
        var act = () => CreateSut().Handle(new BrowseStockMovementsRequest(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Validate_MissingConfirmationId_IsInvalid()
    {
        // Arrange
        var validator = new BrowseStockMovementsRequestValidator();

        // Act
        var result = await validator.ValidateRequestAsync(new BrowseStockMovementsRequest());

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithConfirmationId_IsValid()
    {
        // Arrange
        var validator = new BrowseStockMovementsRequestValidator();

        // Act
        var result = await validator.ValidateRequestAsync(
            new BrowseStockMovementsRequest { ConfirmationId = Guid.NewGuid() });

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
