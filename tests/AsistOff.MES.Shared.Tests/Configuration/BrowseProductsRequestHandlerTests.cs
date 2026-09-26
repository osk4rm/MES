using AsistOff.MES.Configuration.Application.Features.Products.Browse;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class BrowseProductsRequestHandlerTests
{
    private readonly Mock<IProductsRepository> _repository = new();

    private BrowseProductsRequestHandler CreateSut() => new(_repository.Object);

    private (Func<Product, bool> Matches, Paginator<Product>? Captured) Capture()
    {
        Paginator<Product>? captured = null;

        _repository
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Product>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.BrowseAsync(It.IsAny<Paginator<Product>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<Product>, CancellationToken>((paginator, _) => captured = paginator)
            .ReturnsAsync(Array.Empty<Product>());

        return (product =>
        {
            captured.Should().NotBeNull();
            return captured!.Filter.Compile()(product);
        }, captured);
    }

    private static Product MakeProduct(string code, string name = "Widget") =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name
        };

    [Fact]
    public async Task Handle_SearchFilter_MatchesCodeSubstring()
    {
        var (matches, _) = Capture();
        var request = new BrowseProductsRequest { Search = "ABC" };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(MakeProduct("X-ABC-1")).Should().BeTrue();
        matches(MakeProduct("XYZ-1")).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Search_CapsPageAt20_OrderedByCode()
    {
        _repository
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Product>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        Paginator<Product>? captured = null;
        _repository
            .Setup(r => r.BrowseAsync(It.IsAny<Paginator<Product>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<Product>, CancellationToken>((paginator, _) => captured = paginator)
            .ReturnsAsync(Array.Empty<Product>());
        var request = new BrowseProductsRequest { Search = "WID", PageNumber = 1, PageSize = 100 };

        await CreateSut().Handle(request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Paging.PageSize.Should().Be(20);
        captured!.Paging.PageNumber.Should().Be(1);
        captured!.Paging.RawSort.Should().ContainSingle().Which.Should().Be("Code");
    }

    [Fact]
    public async Task Handle_Search_RespectsSmallerRequestedPageSize()
    {
        _repository
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Product>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        Paginator<Product>? captured = null;
        _repository
            .Setup(r => r.BrowseAsync(It.IsAny<Paginator<Product>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<Product>, CancellationToken>((paginator, _) => captured = paginator)
            .ReturnsAsync(Array.Empty<Product>());
        var request = new BrowseProductsRequest { Search = "WID", PageNumber = 1, PageSize = 5 };

        await CreateSut().Handle(request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Paging.PageSize.Should().Be(5);
        captured!.Paging.RawSort.Should().ContainSingle().Which.Should().Be("Code");
    }

    [Fact]
    public async Task Handle_WithoutSearch_UsesRequestedPagingUnchanged()
    {
        _repository
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Product>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        Paginator<Product>? captured = null;
        _repository
            .Setup(r => r.BrowseAsync(It.IsAny<Paginator<Product>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<Product>, CancellationToken>((paginator, _) => captured = paginator)
            .ReturnsAsync(Array.Empty<Product>());
        var request = new BrowseProductsRequest { PageNumber = 2, PageSize = 10 };

        await CreateSut().Handle(request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Paging.Should().BeSameAs(request);
    }
}
