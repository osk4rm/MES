using AsistOff.MES.Production.Application.Features.Lots.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// Unit tests for issue #274: <c>Search</c> typeahead on Lot browse caps the
/// page to 20 matches ordered by code, with server-side MaxPageSize
/// enforcement via the validator.
/// </summary>
public class BrowseLotsSearchTests
{
    private readonly Mock<ILotsRepository> _repository = new();

    private BrowseLotsRequestHandler CreateSut() => new(_repository.Object);

    private static Lot MakeLot(string code) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        ProductId = Guid.NewGuid(),
        MeasureUnitId = Guid.NewGuid(),
        Quantity = 1m,
        Status = LotStatus.Available
    };

    private (Func<Lot, bool> Matches, Paginator<Lot>? Captured) Capture()
    {
        Paginator<Lot>? captured = null;
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Lot>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<Lot>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<Lot>, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(Array.Empty<Lot>());

        return (
            lot =>
            {
                captured.Should().NotBeNull();
                return captured!.Filter.Compile()(lot);
            },
            captured);
    }

    [Fact]
    public async Task Handle_SearchFilter_MatchesCodeSubstring()
    {
        // Arrange
        Paginator<Lot>? captured = null;
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Lot>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<Lot>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<Lot>, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(Array.Empty<Lot>());

        // Act
        await CreateSut().Handle(new BrowseLotsRequest { Search = "ABC" }, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        var matches = captured!.Filter.Compile();
        matches(MakeLot("X-ABC-1")).Should().BeTrue();
        matches(MakeLot("XYZ-1")).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Search_CapsPageSizeTo20_AndOrdersByCode()
    {
        // Arrange
        Paginator<Lot>? captured = null;
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Lot>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<Lot>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<Lot>, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(Array.Empty<Lot>());

        // Act - default PageSize is 50, Search must cap it to 20 with code sort.
        await CreateSut().Handle(new BrowseLotsRequest { Search = "LOT", PageSize = 50, PageNumber = 3 }, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.Paging.PageSize.Should().Be(20);
        captured.Paging.PageNumber.Should().Be(1);
        captured.Paging.RawSort.Should().ContainSingle().Which.Should().Be("Code");
    }

    [Fact]
    public async Task Handle_Search_WithSmallPageSize_KeepsSmallerPage()
    {
        // Arrange
        Paginator<Lot>? captured = null;
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Lot>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<Lot>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<Lot>, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(Array.Empty<Lot>());

        // Act
        await CreateSut().Handle(new BrowseLotsRequest { Search = "LOT", PageSize = 5 }, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.Paging.PageSize.Should().Be(5);
    }

    [Fact]
    public async Task Handle_NoSearch_KeepsOriginalPaging_AndNoForcedSort()
    {
        // Arrange
        Paginator<Lot>? captured = null;
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Lot>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<Lot>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<Lot>, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(Array.Empty<Lot>());

        // Act
        await CreateSut().Handle(new BrowseLotsRequest { PageSize = 50 }, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.Paging.PageSize.Should().Be(50);
        captured.Paging.RawSort.Should().BeEmpty();
    }

    [Fact]
    public void Validator_PageSizeAboveMax_IsInvalid()
    {
        // Arrange
        var validator = new BrowseLotsRequestValidator();

        // Act
        var result = validator.Validate(new BrowseLotsRequest { PageSize = 201, PageNumber = 1 });

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_PageSizeAtMax_IsValid()
    {
        // Arrange
        var validator = new BrowseLotsRequestValidator();

        // Act
        var result = validator.Validate(new BrowseLotsRequest { PageSize = 200, PageNumber = 1 });

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
