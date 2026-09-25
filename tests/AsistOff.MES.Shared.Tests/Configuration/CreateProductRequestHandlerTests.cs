using AsistOff.MES.Configuration.Application.Features.Products.Create;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class CreateProductRequestHandlerTests
{
    private readonly Mock<IProductsRepository> _repository = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<ITenantContext> _tenant = new();

    public CreateProductRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
        _repository
            .Setup(r => r.EanExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product entity, CancellationToken _) => entity);
    }

    private CreateProductRequestHandler CreateSut() =>
        new(_repository.Object, _guids.Object, _tenant.Object, NullLogger<CreateProductRequestHandler>.Instance);

    private static CreateProductRequest Request(string? ean, string code = "EAN-CREATE-1") =>
        new(code, "EAN product", null, ean, null, ScanBy.Code, true, null, null);

    [Fact]
    public async Task Handle_BadCheckDigit_ThrowsValidationExceptionAndStoresNothing()
    {
        // Act
        var act = () => CreateSut().Handle(Request("5901234123458"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _repository.Verify(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateEan_ThrowsConflictExceptionAndStoresNothing()
    {
        // Arrange
        _repository
            .Setup(r => r.EanExistsAsync("5901234123457", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var act = () => CreateSut().Handle(Request("5901234123457"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        _repository.Verify(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhitespacePaddedEan_NormalizesToTrimmed()
    {
        // Arrange
        Product? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((Product entity, CancellationToken _) => entity);

        // Act
        await CreateSut().Handle(Request("  5901234123457  "), CancellationToken.None);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.Ean.Should().Be("5901234123457");
        _repository.Verify(r => r.EanExistsAsync("5901234123457", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_NullOrEmptyEan_PersistsNullWithoutUniquenessCheck(string? ean)
    {
        // Arrange
        Product? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((Product entity, CancellationToken _) => entity);

        // Act
        var result = await CreateSut().Handle(Request(ean), CancellationToken.None);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.Ean.Should().BeNull();
        result.Ean.Should().BeNull();
        _repository.Verify(r => r.EanExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidUniqueEan_PersistsEntity()
    {
        // Arrange
        Product? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((Product entity, CancellationToken _) => entity);

        // Act
        var result = await CreateSut().Handle(Request("5901234123457"), CancellationToken.None);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.Ean.Should().Be("5901234123457");
        result.Ean.Should().Be("5901234123457");
    }
}
