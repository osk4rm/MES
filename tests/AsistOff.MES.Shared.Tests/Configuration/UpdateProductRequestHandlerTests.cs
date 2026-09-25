using AsistOff.MES.Configuration.Application.Features.Products.Update;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class UpdateProductRequestHandlerTests
{
    private readonly Mock<IProductsRepository> _repository = new();

    private static Product ExistingProduct(Guid id, string ean = "5901234123457") => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        Code = "EAN-UPDATE-1",
        Name = "EAN product",
        Ean = ean,
        Barcode = null,
        ScanBy = ScanBy.Code,
        IsActive = true
    };

    private UpdateProductRequestHandler CreateSut() =>
        new(_repository.Object, NullLogger<UpdateProductRequestHandler>.Instance);

    private static UpdateProductRequest Request(Guid id, string? ean) =>
        new(id, "EAN-UPDATE-1", "EAN product", null, ean, null, ScanBy.Code, true, null, null);

    private void SetupExisting(Product product)
    {
        _repository
            .Setup(r => r.GetAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _repository
            .Setup(r => r.EanExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    [Fact]
    public async Task Handle_BadCheckDigit_ThrowsValidationExceptionAndDoesNotUpdate()
    {
        // Arrange
        var product = ExistingProduct(Guid.NewGuid());
        SetupExisting(product);

        // Act
        var act = () => CreateSut().Handle(Request(product.Id, "5901234123458"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateEanOfAnotherProduct_ThrowsConflictException()
    {
        // Arrange
        var product = ExistingProduct(Guid.NewGuid());
        SetupExisting(product);
        _repository
            .Setup(r => r.EanExistsAsync("4006381333931", product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var act = () => CreateSut().Handle(Request(product.Id, "4006381333931"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnchangedEan_SucceedsWithoutConflict()
    {
        // Arrange — the only row holding the EAN is the product itself (excluded).
        var product = ExistingProduct(Guid.NewGuid());
        SetupExisting(product);

        // Act
        var result = await CreateSut().Handle(Request(product.Id, "5901234123457"), CancellationToken.None);

        // Assert
        result.Ean.Should().Be("5901234123457");
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhitespacePaddedEan_NormalizesToTrimmed()
    {
        // Arrange
        var product = ExistingProduct(Guid.NewGuid());
        SetupExisting(product);

        // Act
        var result = await CreateSut().Handle(Request(product.Id, "  4006381333931  "), CancellationToken.None);

        // Assert
        result.Ean.Should().Be("4006381333931");
        product.Ean.Should().Be("4006381333931");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ClearEanToEmpty_NormalizesToNull(string? ean)
    {
        // Arrange
        var product = ExistingProduct(Guid.NewGuid());
        SetupExisting(product);

        // Act
        var result = await CreateSut().Handle(Request(product.Id, ean), CancellationToken.None);

        // Assert
        result.Ean.Should().BeNull();
        product.Ean.Should().BeNull();
        _repository.Verify(r => r.EanExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        // Act
        var act = () => CreateSut().Handle(Request(id, "5901234123457"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
