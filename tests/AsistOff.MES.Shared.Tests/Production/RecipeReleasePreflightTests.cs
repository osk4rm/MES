using AsistOff.MES.Production.Application.Features.RecipeVersions.Release;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// Unit tests for the release preflight rules (issue #388, finding R-7):
/// every rule is covered in its pass, warn and fail paths.
/// </summary>
public class RecipeReleasePreflightTests
{
    private static readonly IReadOnlyDictionary<Guid, bool> NoProducts =
        new Dictionary<Guid, bool>();

    private static readonly IReadOnlySet<Guid> NoWarehouses = new HashSet<Guid>();

    [Fact]
    public void Evaluate_EmptyVersion_OperationsFail()
    {
        // Arrange
        var version = new RecipeVersion { Id = Guid.NewGuid(), RecipeId = Guid.NewGuid(), VersionNumber = 1 };

        // Act
        var checks = RecipeReleasePreflight.Evaluate(version, NoProducts, NoWarehouses);

        // Assert
        var operations = checks.Should().ContainSingle(c => c.Rule == RecipeReleasePreflight.OperationsRule).Subject;
        operations.State.Should().Be(PreflightState.Fail);
        operations.Message.Should().Contain("at least one operation");
        RecipeReleasePreflight.HasFailures(checks).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_HealthyVersion_AllChecksPass()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var version = MakeVersion();
        AddOperation(version, "OP-10", withOutput: (productId, warehouseId), withBom: (productId, warehouseId));

        // Act
        var checks = RecipeReleasePreflight.Evaluate(
            version,
            new Dictionary<Guid, bool> { [productId] = true },
            new HashSet<Guid> { warehouseId });

        // Assert
        checks.Should().HaveCount(6);
        checks.Should().OnlyContain(c => c.State == PreflightState.Pass);
        RecipeReleasePreflight.HasFailures(checks).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NoOutputs_Warn_DoesNotBlock()
    {
        // Arrange
        var version = MakeVersion();
        AddOperation(version, "OP-10", withOutput: null, withBom: null);

        // Act
        var checks = RecipeReleasePreflight.Evaluate(version, NoProducts, NoWarehouses);

        // Assert
        var outputs = checks.Should().ContainSingle(c => c.Rule == RecipeReleasePreflight.OutputsRule).Subject;
        outputs.State.Should().Be(PreflightState.Warn);
        RecipeReleasePreflight.HasFailures(checks).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_IncoherentValidity_Fail()
    {
        // Arrange
        var version = MakeVersion();
        AddOperation(version, "OP-10", withOutput: null, withBom: null);
        version.ValidFrom = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        version.ValidTo = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var checks = RecipeReleasePreflight.Evaluate(version, NoProducts, NoWarehouses);

        // Assert
        var validity = checks.Should().ContainSingle(c => c.Rule == RecipeReleasePreflight.ValidityRule).Subject;
        validity.State.Should().Be(PreflightState.Fail);
        RecipeReleasePreflight.HasFailures(checks).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_CoherentValidity_Pass()
    {
        // Arrange
        var version = MakeVersion();
        AddOperation(version, "OP-10", withOutput: null, withBom: null);
        version.ValidFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        version.ValidTo = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var checks = RecipeReleasePreflight.Evaluate(version, NoProducts, NoWarehouses);

        // Assert
        checks.Should().ContainSingle(c => c.Rule == RecipeReleasePreflight.ValidityRule && c.State == PreflightState.Pass);
    }

    [Fact]
    public void Evaluate_SelfDependency_Fail()
    {
        // Arrange
        var version = MakeVersion();
        var op = AddOperation(version, "OP-10", withOutput: null, withBom: null);
        op.Dependencies.Add(new OperationDependency
        {
            Id = Guid.NewGuid(),
            RecipeVersionId = version.Id,
            OperationNodeId = op.Id,
            PredecessorOperationNodeId = op.Id
        });

        // Act
        var checks = RecipeReleasePreflight.Evaluate(version, NoProducts, NoWarehouses);

        // Assert
        var deps = checks.Should().ContainSingle(c => c.Rule == RecipeReleasePreflight.DependenciesRule).Subject;
        deps.State.Should().Be(PreflightState.Fail);
        deps.Message.Should().Contain("itself");
        RecipeReleasePreflight.HasFailures(checks).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_CrossVersionPredecessor_Fail()
    {
        // Arrange
        var version = MakeVersion();
        var op = AddOperation(version, "OP-10", withOutput: null, withBom: null);
        op.Dependencies.Add(new OperationDependency
        {
            Id = Guid.NewGuid(),
            RecipeVersionId = version.Id,
            OperationNodeId = op.Id,
            PredecessorOperationNodeId = Guid.NewGuid()
        });

        // Act
        var checks = RecipeReleasePreflight.Evaluate(version, NoProducts, NoWarehouses);

        // Assert
        checks.Should().ContainSingle(c => c.Rule == RecipeReleasePreflight.DependenciesRule && c.State == PreflightState.Fail);
        RecipeReleasePreflight.HasFailures(checks).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_CyclicDependencies_Fail()
    {
        // Arrange
        var version = MakeVersion();
        var a = AddOperation(version, "OP-A", withOutput: null, withBom: null);
        var b = AddOperation(version, "OP-B", withOutput: null, withBom: null);
        a.Dependencies.Add(new OperationDependency
        {
            Id = Guid.NewGuid(), RecipeVersionId = version.Id, OperationNodeId = a.Id, PredecessorOperationNodeId = b.Id
        });
        b.Dependencies.Add(new OperationDependency
        {
            Id = Guid.NewGuid(), RecipeVersionId = version.Id, OperationNodeId = b.Id, PredecessorOperationNodeId = a.Id
        });

        // Act
        var checks = RecipeReleasePreflight.Evaluate(version, NoProducts, NoWarehouses);

        // Assert
        var deps = checks.Should().ContainSingle(c => c.Rule == RecipeReleasePreflight.DependenciesRule).Subject;
        deps.State.Should().Be(PreflightState.Fail);
        deps.Message.Should().Contain("cycle");
        RecipeReleasePreflight.HasFailures(checks).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_LinearDependencies_Pass()
    {
        // Arrange
        var version = MakeVersion();
        var a = AddOperation(version, "OP-A", withOutput: null, withBom: null);
        var b = AddOperation(version, "OP-B", withOutput: null, withBom: null);
        b.Dependencies.Add(new OperationDependency
        {
            Id = Guid.NewGuid(), RecipeVersionId = version.Id, OperationNodeId = b.Id, PredecessorOperationNodeId = a.Id
        });

        // Act
        var checks = RecipeReleasePreflight.Evaluate(version, NoProducts, NoWarehouses);

        // Assert
        checks.Should().ContainSingle(c => c.Rule == RecipeReleasePreflight.DependenciesRule && c.State == PreflightState.Pass);
        RecipeReleasePreflight.HasFailures(checks).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_InactiveProduct_Fail()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var version = MakeVersion();
        AddOperation(version, "OP-10", withOutput: (productId, null), withBom: null);

        // Act
        var checks = RecipeReleasePreflight.Evaluate(
            version,
            new Dictionary<Guid, bool> { [productId] = false },
            NoWarehouses);

        // Assert
        var products = checks.Should().ContainSingle(c => c.Rule == RecipeReleasePreflight.ProductsRule).Subject;
        products.State.Should().Be(PreflightState.Fail);
        products.Message.Should().Contain("inactive");
        RecipeReleasePreflight.HasFailures(checks).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_MissingProduct_Warn_DoesNotBlock()
    {
        // Arrange: opaque product ids without a backing row are unverifiable
        // (movement-preview helpers use random guids), so they warn like the
        // frontend checklist instead of blocking the release.
        var version = MakeVersion();
        AddOperation(version, "OP-10", withOutput: null, withBom: (Guid.NewGuid(), null));

        // Act
        var checks = RecipeReleasePreflight.Evaluate(version, NoProducts, NoWarehouses);

        // Assert
        var products = checks.Should().ContainSingle(c => c.Rule == RecipeReleasePreflight.ProductsRule).Subject;
        products.State.Should().Be(PreflightState.Warn);
        RecipeReleasePreflight.HasFailures(checks).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_UnknownWarehouse_Warn_DoesNotBlock()
    {
        // Arrange: unknown warehouse references warn (not fail) to mirror
        // releaseChecklist.ts — the lookup may be capped and ids are opaque.
        var version = MakeVersion();
        AddOperation(version, "OP-10", withOutput: (Guid.NewGuid(), Guid.NewGuid()), withBom: null);

        // Act
        var checks = RecipeReleasePreflight.Evaluate(
            version,
            new Dictionary<Guid, bool>(version.Operations
                .SelectMany(o => o.Outputs.Select(x => x.ProductId))
                .ToDictionary(id => id, _ => true)),
            NoWarehouses);

        // Assert
        var warehouses = checks.Should().ContainSingle(c => c.Rule == RecipeReleasePreflight.WarehousesRule).Subject;
        warehouses.State.Should().Be(PreflightState.Warn);
        RecipeReleasePreflight.HasFailures(checks).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_UnsetWarehouse_Warn_DoesNotBlock()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var version = MakeVersion();
        AddOperation(version, "OP-10", withOutput: (productId, null), withBom: (productId, null));

        // Act
        var checks = RecipeReleasePreflight.Evaluate(
            version,
            new Dictionary<Guid, bool> { [productId] = true },
            NoWarehouses);

        // Assert
        var warehouses = checks.Should().ContainSingle(c => c.Rule == RecipeReleasePreflight.WarehousesRule).Subject;
        warehouses.State.Should().Be(PreflightState.Warn);
        RecipeReleasePreflight.HasFailures(checks).Should().BeFalse();
    }

    private static RecipeVersion MakeVersion() => new()
    {
        Id = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        VersionNumber = 1,
        Status = RecipeVersionStatus.Draft
    };

    private static OperationNode AddOperation(
        RecipeVersion version,
        string code,
        (Guid ProductId, Guid? WarehouseId)? withOutput,
        (Guid ProductId, Guid? WarehouseId)? withBom)
    {
        var op = new OperationNode
        {
            Id = Guid.NewGuid(),
            RecipeVersionId = version.Id,
            Code = code,
            Name = code,
            SortIndex = version.Operations.Count
        };
        if (withOutput is { } output)
        {
            op.Outputs.Add(new OperationOutput
            {
                Id = Guid.NewGuid(),
                OperationNodeId = op.Id,
                ProductId = output.ProductId,
                Quantity = 1,
                PreferredWarehouseId = output.WarehouseId
            });
        }
        if (withBom is { } bom)
        {
            op.BomItems.Add(new BomItem
            {
                Id = Guid.NewGuid(),
                OperationNodeId = op.Id,
                ProductId = bom.ProductId,
                Quantity = 1,
                PreferredWarehouseId = bom.WarehouseId
            });
        }
        version.Operations.Add(op);
        return op;
    }
}
