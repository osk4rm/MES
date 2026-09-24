using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Browse;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class BrowseMaintenanceWorkOrdersRequestHandlerTests
{
    private readonly Mock<IMaintenanceWorkOrdersRepository> _repository = new();

    private BrowseMaintenanceWorkOrdersRequestHandler CreateSut() => new(_repository.Object);

    private Func<MaintenanceWorkOrder, bool> CaptureFilter()
    {
        Paginator<MaintenanceWorkOrder>? captured = null;

        _repository
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<MaintenanceWorkOrder>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.BrowseAsync(It.IsAny<Paginator<MaintenanceWorkOrder>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<MaintenanceWorkOrder>, CancellationToken>((paginator, _) => captured = paginator)
            .ReturnsAsync(Array.Empty<MaintenanceWorkOrder>());

        return order =>
        {
            captured.Should().NotBeNull();
            return captured!.Filter.Compile()(order);
        };
    }

    private static MaintenanceWorkOrder Order(Guid machineId, MaintenanceWorkOrderStatus status) => new()
    {
        Code = "WO-1",
        Title = "Fix spindle",
        MachineId = machineId,
        Priority = MaintenanceWorkOrderPriority.High,
        Status = status,
        ReportedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Handle_MachineIdFilter_OnlyMatchesRequestedMachine()
    {
        var matches = CaptureFilter();
        var machineId = Guid.NewGuid();
        var request = new BrowseMaintenanceWorkOrdersRequest { MachineId = machineId };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(Order(machineId, MaintenanceWorkOrderStatus.Open)).Should().BeTrue();
        matches(Order(Guid.NewGuid(), MaintenanceWorkOrderStatus.Open)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_StatusFilter_OnlyMatchesRequestedStatus()
    {
        var matches = CaptureFilter();
        var machineId = Guid.NewGuid();
        var request = new BrowseMaintenanceWorkOrdersRequest { Status = MaintenanceWorkOrderStatus.Done };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(Order(machineId, MaintenanceWorkOrderStatus.Done)).Should().BeTrue();
        matches(Order(machineId, MaintenanceWorkOrderStatus.Open)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoFilters_MatchesEveryWorkOrder()
    {
        var matches = CaptureFilter();
        var request = new BrowseMaintenanceWorkOrdersRequest();

        await CreateSut().Handle(request, CancellationToken.None);

        matches(Order(Guid.NewGuid(), MaintenanceWorkOrderStatus.InProgress)).Should().BeTrue();
    }
}
