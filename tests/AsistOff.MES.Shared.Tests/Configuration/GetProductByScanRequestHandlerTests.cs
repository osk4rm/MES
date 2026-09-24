using AsistOff.MES.Configuration.Application.Features.Products.ByScan;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class GetProductByScanRequestHandlerTests
{
    private readonly Mock<IProductsRepository> _repository = new();

    private static Product ActiveProduct(string code, string? ean = null, string? barcode = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Code = code,
        Name = $"Product {code}",
        Ean = ean,
        Barcode = barcode,
        ScanBy = ScanBy.Code,
        IsActive = true
    };

    private GetProductByScanRequestHandler CreateSut() => new(_repository.Object);

    [Fact]
    public async Task Handle_CodeHit_ReturnsProduct()
    {
        // Arrange
        var product = ActiveProduct("SCAN-CODE-1", "5901234123457", "BC-1");
        _repository
            .Setup(r => r.GetByScanAsync("SCAN-CODE-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await CreateSut().Handle(new GetProductByScanRequest("SCAN-CODE-1"), CancellationToken.None);

        // Assert
        result.Code.Should().Be("SCAN-CODE-1");
        result.Ean.Should().Be("5901234123457");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EanHit_ReturnsProduct()
    {
        // Arrange
        var product = ActiveProduct("PROD-EAN", "5909876543210");
        _repository
            .Setup(r => r.GetByScanAsync("5909876543210", It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await CreateSut().Handle(new GetProductByScanRequest("5909876543210"), CancellationToken.None);

        // Assert
        result.Code.Should().Be("PROD-EAN");
        result.Ean.Should().Be("5909876543210");
    }

    [Fact]
    public async Task Handle_BarcodeHit_ReturnsProduct()
    {
        // Arrange
        var product = ActiveProduct("PROD-BC", null, "INT-BARCODE-9");
        _repository
            .Setup(r => r.GetByScanAsync("INT-BARCODE-9", It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await CreateSut().Handle(new GetProductByScanRequest("INT-BARCODE-9"), CancellationToken.None);

        // Assert
        result.Code.Should().Be("PROD-BC");
        result.Barcode.Should().Be("INT-BARCODE-9");
    }

    [Fact]
    public async Task Handle_UnknownValue_ThrowsNotFoundException()
    {
        // Arrange
        _repository
            .Setup(r => r.GetByScanAsync("NO-SUCH-CODE", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        // Act
        var act = () => CreateSut().Handle(new GetProductByScanRequest("NO-SUCH-CODE"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValueBelongingOnlyToInactiveProduct_ThrowsNotFoundException()
    {
        // Arrange — the repository only matches active products, so an
        // inactive product's code resolves to null.
        _repository
            .Setup(r => r.GetByScanAsync("INACTIVE-CODE", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        // Act
        var act = () => CreateSut().Handle(new GetProductByScanRequest("INACTIVE-CODE"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NullValue_ThrowsValidationException()
    {
        // Arrange
        var request = new GetProductByScanRequest(null);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EmptyValue_ThrowsValidationException()
    {
        // Arrange
        var request = new GetProductByScanRequest(string.Empty);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WhitespaceValue_ThrowsValidationException()
    {
        // Arrange
        var request = new GetProductByScanRequest("   ");

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_ValueWithSurroundingWhitespace_TrimsBeforeLookup()
    {
        // Arrange
        var product = ActiveProduct("TRIMMED-CODE");
        _repository
            .Setup(r => r.GetByScanAsync("TRIMMED-CODE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await CreateSut().Handle(new GetProductByScanRequest("  TRIMMED-CODE\t"), CancellationToken.None);

        // Assert
        result.Code.Should().Be("TRIMMED-CODE");
        _repository.Verify(r => r.GetByScanAsync("TRIMMED-CODE", It.IsAny<CancellationToken>()), Times.Once);
    }
}
