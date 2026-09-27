using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Application.Features.RecipeVersions.Release;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// Handler-level wiring for the server-side release preflight recheck
/// (issue #388): fail-state rules surface as structured 400s, warn-state
/// rules do not block the release.
/// </summary>
public class ReleaseRecipeVersionPreflightHandlerTests
{
    private readonly Mock<IRecipeVersionsRepository> _versions = new();
    private readonly Mock<IRecipesRepository> _recipes = new();
    private readonly Mock<IProductsRepository> _products = new();
    private readonly Mock<IWarehousesRepository> _warehouses = new();
    private readonly Mock<IDateTimeProvider> _clock = new();

    public ReleaseRecipeVersionPreflightHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);
        _warehouses.Setup(w => w.BrowseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Warehouse>());
    }

    private ReleaseRecipeVersionRequestHandler CreateSut() =>
        new(_versions.Object, _recipes.Object, _products.Object, _warehouses.Object, _clock.Object);

    [Fact]
    public async Task Throws_ValidationException_WithValidityKey_when_dates_incoherent()
    {
        // Arrange
        var version = MakeVersion();
        AddOperation(version, "OP-10", outputProductId: null, bomProductId: null);
        version.ValidFrom = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        version.ValidTo = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _versions.Setup(v => v.GetFullAsync(version.Id, It.IsAny<CancellationToken>())).ReturnsAsync(version);

        // Act
        var act = () => CreateSut().Handle(new ReleaseRecipeVersionRequest(version.Id), CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainKey(RecipeReleasePreflight.ValidityRule);
    }

    [Fact]
    public async Task Throws_ValidationException_WithProductsKey_when_bom_product_inactive()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var version = MakeVersion();
        AddOperation(version, "OP-10", outputProductId: null, bomProductId: productId);
        _versions.Setup(v => v.GetFullAsync(version.Id, It.IsAny<CancellationToken>())).ReturnsAsync(version);
        _products.Setup(p => p.GetAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Product { Id = productId, Code = "MAT", Name = "MAT", IsActive = false });

        // Act
        var act = () => CreateSut().Handle(new ReleaseRecipeVersionRequest(version.Id), CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainKey(RecipeReleasePreflight.ProductsRule);
    }

    [Fact]
    public async Task Releases_when_warehouse_unknown_warn_only()
    {
        // Arrange: unknown warehouse references are warn-only (opaque ids,
        // mirrors releaseChecklist.ts) and must not block the release.
        // Regression cover for the ConfirmationMovements / MaterialReservations
        // / Stock integration helpers, which release versions with random
        // warehouse guids.
        var productId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var version = MakeVersion(recipeId);
        var op = AddOperation(version, "OP-10", outputProductId: productId, bomProductId: null);
        op.Outputs.Single().PreferredWarehouseId = Guid.NewGuid();
        var recipe = new Recipe { Id = recipeId, Code = "R", Name = "R" };
        _versions.Setup(v => v.GetFullAsync(version.Id, It.IsAny<CancellationToken>())).ReturnsAsync(version);
        _versions.Setup(v => v.ListForRecipeAsync(recipeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { version });
        _recipes.Setup(r => r.GetAsync(recipeId, It.IsAny<CancellationToken>())).ReturnsAsync(recipe);
        _products.Setup(p => p.GetAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Product { Id = productId, Code = "P", Name = "P", IsActive = true });

        // Act
        await CreateSut().Handle(new ReleaseRecipeVersionRequest(version.Id), CancellationToken.None);

        // Assert
        version.Status.Should().Be(RecipeVersionStatus.Released);
    }

    [Fact]
    public async Task Releases_when_product_missing_warn_only()
    {
        // Arrange: referenced products without a backing row are unverifiable
        // (opaque ids) and must not block the release; only inactive products fail.
        var recipeId = Guid.NewGuid();
        var version = MakeVersion(recipeId);
        AddOperation(version, "OP-10", outputProductId: null, bomProductId: Guid.NewGuid());
        var recipe = new Recipe { Id = recipeId, Code = "R", Name = "R" };
        _versions.Setup(v => v.GetFullAsync(version.Id, It.IsAny<CancellationToken>())).ReturnsAsync(version);
        _versions.Setup(v => v.ListForRecipeAsync(recipeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { version });
        _recipes.Setup(r => r.GetAsync(recipeId, It.IsAny<CancellationToken>())).ReturnsAsync(recipe);
        _products.Setup(p => p.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        // Act
        await CreateSut().Handle(new ReleaseRecipeVersionRequest(version.Id), CancellationToken.None);

        // Assert
        version.Status.Should().Be(RecipeVersionStatus.Released);
    }

    [Fact]
    public async Task Releases_when_only_warn_rules_trigger()
    {
        // Arrange: one bare operation — no outputs (warn) and no warehouses (pass, vacuous).
        var recipeId = Guid.NewGuid();
        var version = MakeVersion(recipeId);
        AddOperation(version, "OP-10", outputProductId: null, bomProductId: null);
        var recipe = new Recipe { Id = recipeId, Code = "R", Name = "R" };
        _versions.Setup(v => v.GetFullAsync(version.Id, It.IsAny<CancellationToken>())).ReturnsAsync(version);
        _versions.Setup(v => v.ListForRecipeAsync(recipeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { version });
        _recipes.Setup(r => r.GetAsync(recipeId, It.IsAny<CancellationToken>())).ReturnsAsync(recipe);

        // Act
        await CreateSut().Handle(new ReleaseRecipeVersionRequest(version.Id), CancellationToken.None);

        // Assert
        version.Status.Should().Be(RecipeVersionStatus.Released);
    }

    private static RecipeVersion MakeVersion(Guid? recipeId = null) => new()
    {
        Id = Guid.NewGuid(),
        RecipeId = recipeId ?? Guid.NewGuid(),
        VersionNumber = 1,
        Status = RecipeVersionStatus.Draft
    };

    private static OperationNode AddOperation(
        RecipeVersion version, string code, Guid? outputProductId, Guid? bomProductId)
    {
        var op = new OperationNode
        {
            Id = Guid.NewGuid(),
            RecipeVersionId = version.Id,
            Code = code,
            Name = code,
            SortIndex = version.Operations.Count
        };
        if (outputProductId.HasValue)
        {
            op.Outputs.Add(new OperationOutput
            {
                Id = Guid.NewGuid(),
                OperationNodeId = op.Id,
                ProductId = outputProductId.Value,
                Quantity = 1
            });
        }
        if (bomProductId.HasValue)
        {
            op.BomItems.Add(new BomItem
            {
                Id = Guid.NewGuid(),
                OperationNodeId = op.Id,
                ProductId = bomProductId.Value,
                Quantity = 1
            });
        }
        version.Operations.Add(op);
        return op;
    }
}
