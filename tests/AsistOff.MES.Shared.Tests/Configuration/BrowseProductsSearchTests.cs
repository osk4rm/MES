using AsistOff.MES.Configuration.Application.Features.Products.Browse;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

/// <summary>
/// Unit tests for issue #274: <c>Search</c> typeahead on Product browse caps
/// the page to 20 matches ordered by code, with server-side MaxPageSize
/// enforcement via the validator.
/// </summary>
public class BrowseProductsSearchTests
{
    private readonly Mock<IProductsRepository> _repository = new();

    private BrowseProductsRequestHandler CreateSut() => new(_repository.Object);

    private static Product MakeProduct(string code) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = $"Name {code}",
        IsActive = true
    };

    [Fact]
    public async Task Handle_SearchFilter_MatchesCodeSubstring()
    {
        // Arrange
        Paginator<Product>? captured = null;
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Product>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<Product>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<Product>, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(Array.Empty<Product>());

        // Act
        await CreateSut().Handle(new BrowseProductsRequest { Search = "ABC" }, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        var matches = captured!.Filter.Compile();
        matches(MakeProduct("X-ABC-1")).Should().BeTrue();
        matches(MakeProduct("XYZ-1")).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Search_CapsPageSizeTo20_AndOrdersByCode()
    {
        // Arrange
        Paginator<Product>? captured = null;
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Product>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<Product>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<Product>, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(Array.Empty<Product>());

        // Act - default PageSize is 10, Search still forces code sort; large
        // PageSize values are capped to 20.
        await CreateSut().Handle(new BrowseProductsRequest { Search = "PRD", PageSize = 50 }, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.Paging.PageSize.Should().Be(20);
        captured.Paging.RawSort.Should().ContainSingle().Which.Should().Be("Code");
    }

    [Fact]
    public async Task Handle_NoSearch_KeepsOriginalPaging_AndNoForcedSort()
    {
        // Arrange
        Paginator<Product>? captured = null;
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Product>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<Product>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<Product>, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(Array.Empty<Product>());

        // Act
        await CreateSut().Handle(new BrowseProductsRequest { PageSize = 10 }, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.Paging.PageSize.Should().Be(10);
        captured.Paging.RawSort.Should().BeEmpty();
    }

    [Fact]
    public void Validator_PageSizeAboveMax_IsInvalid()
    {
        // Arrange
        var validator = new BrowseProductsRequestValidator();

        // Act
        var result = validator.Validate(new BrowseProductsRequest { PageSize = 101, PageNumber = 1 });

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_PageSizeAtMax_IsValid()
    {
        // Arrange
        var validator = new BrowseProductsRequestValidator();

        // Act
        var result = validator.Validate(new BrowseProductsRequest { PageSize = 100, PageNumber = 1 });

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
