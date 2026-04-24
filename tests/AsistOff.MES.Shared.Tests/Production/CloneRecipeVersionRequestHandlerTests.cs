using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.RecipeVersions.Clone;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class CloneRecipeVersionRequestHandlerTests
{
    private readonly Mock<IRecipeVersionsRepository> _versions = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();

    public CloneRecipeVersionRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);
        _tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
    }

    private CloneRecipeVersionRequestHandler CreateSut() =>
        new(_versions.Object, _guids.Object, _clock.Object, _tenant.Object);

    [Fact]
    public async Task Throws_NotFoundException_when_source_missing()
    {
        _versions.Setup(v => v.GetFullAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecipeVersion?)null);

        var act = () => CreateSut().Handle(
            new CloneRecipeVersionRequest(Guid.NewGuid(), null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Deep_copies_operations_dependencies_and_child_collections()
    {
        var recipeId = Guid.NewGuid();
        var source = BuildSource(recipeId);

        _versions.Setup(v => v.GetFullAsync(source.Id, It.IsAny<CancellationToken>())).ReturnsAsync(source);
        _versions.Setup(v => v.GetNextVersionNumberAsync(recipeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(source.VersionNumber + 1);

        RecipeVersion? added = null;
        _versions.Setup(v => v.AddAsync(It.IsAny<RecipeVersion>(), It.IsAny<CancellationToken>()))
            .Callback<RecipeVersion, CancellationToken>((rv, _) => added = rv)
            .ReturnsAsync((RecipeVersion rv, CancellationToken _) => rv);
        _versions.Setup(v => v.GetFullAsync(It.Is<Guid>(id => id != source.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => added);

        await CreateSut().Handle(new CloneRecipeVersionRequest(source.Id, "notes", null, null), CancellationToken.None);

        added.Should().NotBeNull();
        added!.RecipeId.Should().Be(recipeId);
        added.Status.Should().Be(RecipeVersionStatus.Draft);
        added.VersionNumber.Should().Be(source.VersionNumber + 1);
        added.Operations.Should().HaveCount(source.Operations.Count);

        // Cloned operations must have fresh IDs, not the source's.
        var sourceIds = source.Operations.Select(o => o.Id).ToHashSet();
        added.Operations.Select(o => o.Id).Should().NotIntersectWith(sourceIds);

        // Dependencies must be rewired to the new IDs.
        var sourceWithDep = source.Operations.Single(o => o.Dependencies.Any());
        var addedWithDep = added.Operations.Single(o => o.Dependencies.Any());
        addedWithDep.Dependencies.Single().PredecessorOperationNodeId
            .Should().NotBe(sourceWithDep.Dependencies.Single().PredecessorOperationNodeId);
        added.Operations.Select(o => o.Id).Should().Contain(addedWithDep.Dependencies.Single().PredecessorOperationNodeId);

        // Child collections deep-copied with new IDs.
        var addedBomOp = added.Operations.Single(o => o.BomItems.Any());
        addedBomOp.BomItems.Single().ProductId.Should().Be(
            source.Operations.Single(o => o.BomItems.Any()).BomItems.Single().ProductId);
    }

    private static RecipeVersion BuildSource(Guid recipeId)
    {
        var op1 = new OperationNode
        {
            Id = Guid.NewGuid(), RecipeVersionId = Guid.NewGuid(),
            Code = "OP1", Name = "Setup", SortIndex = 0
        };
        var op2 = new OperationNode
        {
            Id = Guid.NewGuid(), RecipeVersionId = op1.RecipeVersionId,
            Code = "OP2", Name = "Run", SortIndex = 1
        };
        op2.Dependencies.Add(new OperationDependency
        {
            Id = Guid.NewGuid(),
            RecipeVersionId = op1.RecipeVersionId,
            OperationNodeId = op2.Id,
            PredecessorOperationNodeId = op1.Id,
            DependencyType = OperationDependencyType.FinishToStart
        });
        op2.BomItems.Add(new BomItem
        {
            Id = Guid.NewGuid(),
            OperationNodeId = op2.Id,
            ProductId = Guid.NewGuid(),
            Quantity = 3m,
            QuantityType = BomQuantityType.PerUnit
        });

        return new RecipeVersion
        {
            Id = op1.RecipeVersionId,
            RecipeId = recipeId,
            VersionNumber = 1,
            Status = RecipeVersionStatus.Released,
            Operations = new List<OperationNode> { op1, op2 }
        };
    }
}
