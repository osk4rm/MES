using AsistOff.MES.Production.Application.Features.SpcCharacteristics.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class BrowseSpcCharacteristicsRequestHandlerTests
{
    private readonly Mock<ISpcCharacteristicsRepository> _repository = new();

    private BrowseSpcCharacteristicsRequestHandler CreateSut() => new(_repository.Object);

    private Func<SpcCharacteristic, bool> CaptureFilter()
    {
        Paginator<SpcCharacteristic>? captured = null;

        _repository
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<SpcCharacteristic>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.BrowseAsync(It.IsAny<Paginator<SpcCharacteristic>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<SpcCharacteristic>, CancellationToken>((paginator, _) => captured = paginator)
            .ReturnsAsync(Array.Empty<SpcCharacteristic>());

        return entity =>
        {
            captured.Should().NotBeNull();
            return captured!.Filter.Compile()(entity);
        };
    }

    [Fact]
    public async Task Handle_SearchFilter_MatchesCodeOrName()
    {
        var matches = CaptureFilter();
        var request = new BrowseSpcCharacteristicsRequest { Search = "Shaft" };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(new SpcCharacteristic { Code = "SPC-DIA", Name = "Shaft diameter" })
            .Should().BeTrue();
        matches(new SpcCharacteristic { Code = "SPC-RUN", Name = "Shaft runout" })
            .Should().BeTrue();
        matches(new SpcCharacteristic { Code = "SPC-WGT", Name = "Fill weight" })
            .Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ProductIdFilter_OnlyMatchesRequestedProduct()
    {
        var productId = Guid.NewGuid();
        var matches = CaptureFilter();
        var request = new BrowseSpcCharacteristicsRequest { ProductId = productId };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(new SpcCharacteristic { Code = "A", Name = "A", ProductId = productId })
            .Should().BeTrue();
        matches(new SpcCharacteristic { Code = "B", Name = "B", ProductId = Guid.NewGuid() })
            .Should().BeFalse();
    }

    [Fact]
    public async Task Handle_MachineIdFilter_OnlyMatchesRequestedMachine()
    {
        var machineId = Guid.NewGuid();
        var matches = CaptureFilter();
        var request = new BrowseSpcCharacteristicsRequest { MachineId = machineId };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(new SpcCharacteristic { Code = "A", Name = "A", MachineId = machineId })
            .Should().BeTrue();
        matches(new SpcCharacteristic { Code = "B", Name = "B", MachineId = Guid.NewGuid() })
            .Should().BeFalse();
    }

    [Fact]
    public async Task Handle_IsActiveFilter_OnlyMatchesRequestedState()
    {
        var matches = CaptureFilter();
        var request = new BrowseSpcCharacteristicsRequest { IsActive = false };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(new SpcCharacteristic { Code = "A", Name = "A", IsActive = false })
            .Should().BeTrue();
        matches(new SpcCharacteristic { Code = "B", Name = "B", IsActive = true })
            .Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoFilters_MatchesEveryCharacteristic()
    {
        var matches = CaptureFilter();
        var request = new BrowseSpcCharacteristicsRequest();

        await CreateSut().Handle(request, CancellationToken.None);

        matches(new SpcCharacteristic { Code = "A", Name = "Any" })
            .Should().BeTrue();
    }
}
