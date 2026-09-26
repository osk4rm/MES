using AsistOff.MES.Configuration.Application.Features.MaterialReservations;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

/// <summary>
/// Unit tests for the reservation browse/get reads (issue #291): paging,
/// mapping (including the remaining quantity) and the 404/400 guards.
/// Filter narrowing is covered by the endpoint integration tests.
/// </summary>
public class MaterialReservationsHandlerTests
{
    private readonly Mock<IMaterialReservationsRepository> _reservations = new();

    private static MaterialReservation Row(
        Guid orderId, Guid productId, Guid? warehouseId,
        decimal reserved, decimal relieved, ReservationStatus status) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        ProductionOrderId = orderId,
        ProductId = productId,
        WarehouseId = warehouseId,
        QuantityReserved = reserved,
        QuantityRelieved = relieved,
        Status = status,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public void BrowseRequest_ImplementsTenantRequest()
    {
        typeof(BrowseMaterialReservationsRequest).Should()
            .Implement<ITenantRequest<PagedResponse<MaterialReservationResponse>>>();
    }

    [Fact]
    public void GetRequest_ImplementsTenantRequest()
    {
        typeof(GetMaterialReservationRequest).Should()
            .Implement<ITenantRequest<MaterialReservationResponse>>();
    }

    [Fact]
    public async Task Browse_ReturnsMappedPageWithTotal()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var rows = new List<MaterialReservation>
        {
            Row(orderId, Guid.NewGuid(), null, 200m, 20m, ReservationStatus.PartiallyRelieved),
            Row(orderId, Guid.NewGuid(), Guid.NewGuid(), 50m, 50m, ReservationStatus.Closed)
        };
        _reservations.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<MaterialReservation>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _reservations.Setup(r => r.BrowseAsync(It.IsAny<Paginator<MaterialReservation>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);
        var handler = new BrowseMaterialReservationsRequestHandler(_reservations.Object);

        // Act
        var result = await handler.Handle(
            new BrowseMaterialReservationsRequest { ProductionOrderId = orderId }, CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.First().RemainingQuantity.Should().Be(180m);
        result.Items.First().Status.Should().Be(ReservationStatus.PartiallyRelieved);
        result.Items.Last().RemainingQuantity.Should().Be(0m);
    }

    [Fact]
    public async Task Get_ReturnsMappedReservation()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var row = Row(orderId, productId, null, 200m, 20m, ReservationStatus.Active);
        _reservations.Setup(r => r.GetAsync(row.Id, It.IsAny<CancellationToken>())).ReturnsAsync(row);
        var handler = new GetMaterialReservationRequestHandler(_reservations.Object);

        // Act
        var result = await handler.Handle(new GetMaterialReservationRequest(row.Id), CancellationToken.None);

        // Assert
        result.Should().Be(new MaterialReservationResponse(
            row.Id, orderId, productId, null, 200m, 20m, 180m,
            ReservationStatus.Active, row.CreatedAt, null));
    }

    [Fact]
    public async Task Get_UnknownId_ThrowsNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _reservations.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MaterialReservation?)null);
        var handler = new GetMaterialReservationRequestHandler(_reservations.Object);

        // Act
        var act = () => handler.Handle(new GetMaterialReservationRequest(id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Get_EmptyId_ThrowsValidationException()
    {
        // Arrange
        var handler = new GetMaterialReservationRequestHandler(_reservations.Object);

        // Act
        var act = () => handler.Handle(new GetMaterialReservationRequest(Guid.Empty), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Validate_EmptyGuidFilters_AreInvalid()
    {
        // Arrange
        var validator = new BrowseMaterialReservationsRequestValidator();

        // Act
        var result = await validator.ValidateRequestAsync(new BrowseMaterialReservationsRequest
        {
            ProductionOrderId = Guid.Empty,
            ProductId = Guid.Empty,
            WarehouseId = Guid.Empty
        });

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_BadPaging_IsInvalid()
    {
        // Arrange
        var validator = new BrowseMaterialReservationsRequestValidator();

        // Act
        var result = await validator.ValidateRequestAsync(new BrowseMaterialReservationsRequest
        {
            PageNumber = 0,
            PageSize = 101
        });

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_FiltersAndPaging_AreValid()
    {
        // Arrange
        var validator = new BrowseMaterialReservationsRequestValidator();

        // Act
        var result = await validator.ValidateRequestAsync(new BrowseMaterialReservationsRequest
        {
            ProductionOrderId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Status = ReservationStatus.Active,
            PageNumber = 2,
            PageSize = 25
        });

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
