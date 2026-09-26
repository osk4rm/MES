using AsistOff.MES.Production.Application.Features.Lots.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class BrowseLotsRequestHandlerTests
{
    private readonly Mock<ILotsRepository> _repository = new();

    private BrowseLotsRequestHandler CreateSut() => new(_repository.Object);

    private Func<Lot, bool> CaptureFilter()
    {
        Paginator<Lot>? captured = null;

        _repository
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Lot>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.BrowseAsync(It.IsAny<Paginator<Lot>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<Lot>, CancellationToken>((paginator, _) => captured = paginator)
            .ReturnsAsync(Array.Empty<Lot>());

        return lot =>
        {
            captured.Should().NotBeNull();
            return captured!.Filter.Compile()(lot);
        };
    }

    private static Lot MakeLot(string code, Guid? productId = null, LotStatus status = LotStatus.Available, DateTime? expiry = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            ProductId = productId ?? Guid.NewGuid(),
            MeasureUnitId = Guid.NewGuid(),
            Quantity = 1m,
            Status = status,
            ExpiryDate = expiry
        };

    [Fact]
    public async Task Handle_CodeFilter_OnlyMatchesRequestedCode()
    {
        var matches = CaptureFilter();
        var request = new BrowseLotsRequest { Code = "ABC" };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(MakeLot("ABC-1")).Should().BeTrue();
        matches(MakeLot("XYZ-1")).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ProductFilter_OnlyMatchesRequestedProduct()
    {
        var matches = CaptureFilter();
        var productId = Guid.NewGuid();
        var request = new BrowseLotsRequest { ProductId = productId };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(MakeLot("A", productId)).Should().BeTrue();
        matches(MakeLot("B", Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_StatusFilter_OnlyMatchesRequestedStatus()
    {
        var matches = CaptureFilter();
        var request = new BrowseLotsRequest { Status = LotStatus.OnHold };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(MakeLot("A", status: LotStatus.OnHold)).Should().BeTrue();
        matches(MakeLot("B", status: LotStatus.Available)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ExpiryRangeFilter_OnlyMatchesWithinRange()
    {
        var matches = CaptureFilter();
        var request = new BrowseLotsRequest
        {
            ExpiryFrom = new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            ExpiryTo = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(MakeLot("IN", expiry: new DateTime(2026, 06, 01, 0, 0, 0, DateTimeKind.Utc))).Should().BeTrue();
        matches(MakeLot("OUT", expiry: new DateTime(2027, 06, 01, 0, 0, 0, DateTimeKind.Utc))).Should().BeFalse();
        matches(MakeLot("NONE")).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SearchFilter_MatchesCodeSubstring()
    {
        var matches = CaptureFilter();
        var request = new BrowseLotsRequest { Search = "ABC" };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(MakeLot("X-ABC-1")).Should().BeTrue();
        matches(MakeLot("XYZ-1")).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Search_CapsPageAt20_OrderedByCode()
    {
        Paginator<Lot>? captured = null;
        _repository
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Lot>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.BrowseAsync(It.IsAny<Paginator<Lot>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<Lot>, CancellationToken>((paginator, _) => captured = paginator)
            .ReturnsAsync(Array.Empty<Lot>());
        var request = new BrowseLotsRequest { Search = "LOT", PageNumber = 1, PageSize = 500 };

        await CreateSut().Handle(request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Paging.PageSize.Should().Be(20);
        captured!.Paging.PageNumber.Should().Be(1);
        captured!.Paging.RawSort.Should().ContainSingle().Which.Should().Be("Code");
    }

    [Fact]
    public async Task Handle_Search_RespectsSmallerRequestedPageSize()
    {
        Paginator<Lot>? captured = null;
        _repository
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Lot>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.BrowseAsync(It.IsAny<Paginator<Lot>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<Lot>, CancellationToken>((paginator, _) => captured = paginator)
            .ReturnsAsync(Array.Empty<Lot>());
        var request = new BrowseLotsRequest { Search = "LOT", PageNumber = 1, PageSize = 5 };

        await CreateSut().Handle(request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Paging.PageSize.Should().Be(5);
        captured!.Paging.RawSort.Should().ContainSingle().Which.Should().Be("Code");
    }

    [Fact]
    public async Task Handle_WithoutSearch_UsesRequestedPagingUnchanged()
    {
        Paginator<Lot>? captured = null;
        _repository
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Lot>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.BrowseAsync(It.IsAny<Paginator<Lot>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<Lot>, CancellationToken>((paginator, _) => captured = paginator)
            .ReturnsAsync(Array.Empty<Lot>());
        var request = new BrowseLotsRequest { PageNumber = 2, PageSize = 50 };

        await CreateSut().Handle(request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Paging.Should().BeSameAs(request);
    }
}
