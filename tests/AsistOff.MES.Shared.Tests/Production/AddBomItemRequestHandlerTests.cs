using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.BomItems.Add;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// Verifies that <see cref="AddBomItemRequestHandler"/> uses the direct DbSet.Add
/// pattern (via <see cref="IChildEntitiesRepository.AddBomItemAsync"/>) — the same
/// pattern AddOperationOutput / AddResourceRequirement use — instead of mutating the
/// navigation collection of a tracked principal, which produced the
/// DbUpdateConcurrencyException reported by the user.
/// </summary>
public class AddBomItemRequestHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IChildEntitiesRepository> ChildRepo { get; } = new();
        public Mock<IOperationNodesRepository> OpRepo { get; } = new();
        public Mock<IRecipeVersionsRepository> VersionRepo { get; } = new();
        public Mock<IGuidProvider> Guids { get; } = new();
        public Mock<ITenantContext> Tenant { get; } = new();

        public Fixture()
        {
            Tenant.SetupGet(t => t.TenantId).Returns(TenantId);
            Guids.Setup(g => g.NewGuid()).Returns(Guid.NewGuid);
            ChildRepo.Setup(r => r.AddBomItemAsync(It.IsAny<BomItem>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        public AddBomItemRequestHandler CreateSut() =>
            new(ChildRepo.Object, OpRepo.Object, VersionRepo.Object, Guids.Object, Tenant.Object);
    }

    private static (RecipeVersion version, OperationNode op) BuildOp(RecipeVersionStatus status = RecipeVersionStatus.Draft)
    {
        var version = new RecipeVersion
        {
            Id = Guid.NewGuid(),
            RecipeId = Guid.NewGuid(),
            VersionNumber = 1,
            Status = status,
            TenantId = TenantId
        };
        var op = new OperationNode
        {
            Id = Guid.NewGuid(),
            RecipeVersionId = version.Id,
            TenantId = TenantId,
            Code = "OP1",
            Name = "Mix"
        };
        return (version, op);
    }

    private static void Wire(Fixture f, RecipeVersion version, OperationNode op, int? maxSortIndex = null)
    {
        f.OpRepo.Setup(r => r.GetAsync(op.Id, It.IsAny<CancellationToken>())).ReturnsAsync(op);
        f.VersionRepo.Setup(r => r.GetAsync(version.Id, It.IsAny<CancellationToken>())).ReturnsAsync(version);
        f.ChildRepo.Setup(r => r.GetMaxBomItemSortIndexAsync(op.Id, It.IsAny<CancellationToken>())).ReturnsAsync(maxSortIndex);
    }

    [Fact]
    public async Task Adds_bom_item_via_child_repository_with_direct_dbset_add()
    {
        var f = new Fixture();
        var (version, op) = BuildOp();
        Wire(f, version, op);

        var newId = Guid.NewGuid();
        f.Guids.Setup(g => g.NewGuid()).Returns(newId);

        var productId = Guid.NewGuid();
        var measureUnitId = Guid.NewGuid();

        BomItem? captured = null;
        f.ChildRepo.Setup(r => r.AddBomItemAsync(It.IsAny<BomItem>(), It.IsAny<CancellationToken>()))
            .Callback<BomItem, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var resultId = await f.CreateSut().Handle(
            new AddBomItemRequest(
                op.Id,
                productId,
                measureUnitId,
                Quantity: 2.5m,
                QuantityType: BomQuantityType.PerUnit,
                ScrapPercentage: null,
                IsOptional: false,
                PreferredWarehouseId: null,
                ConsumptionTiming: ConsumptionTiming.AtStart,
                Notes: null,
                SortIndex: null),
            CancellationToken.None);

        resultId.Should().Be(newId);
        captured.Should().NotBeNull();
        captured!.Id.Should().Be(newId);
        captured.OperationNodeId.Should().Be(op.Id);
        captured.TenantId.Should().Be(TenantId);
        captured.ProductId.Should().Be(productId);
        captured.MeasureUnitId.Should().Be(measureUnitId);
        captured.Quantity.Should().Be(2.5m);
        captured.SortIndex.Should().Be(0, "no existing rows -> first SortIndex is 0");
        f.ChildRepo.Verify(r => r.AddBomItemAsync(It.IsAny<BomItem>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Auto_increments_sort_index_from_max()
    {
        var f = new Fixture();
        var (version, op) = BuildOp();
        Wire(f, version, op, maxSortIndex: 7);

        BomItem? captured = null;
        f.ChildRepo.Setup(r => r.AddBomItemAsync(It.IsAny<BomItem>(), It.IsAny<CancellationToken>()))
            .Callback<BomItem, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        await f.CreateSut().Handle(
            new AddBomItemRequest(
                op.Id, Guid.NewGuid(), null,
                Quantity: 1m,
                QuantityType: BomQuantityType.PerBatch,
                ScrapPercentage: null,
                IsOptional: false,
                PreferredWarehouseId: null,
                ConsumptionTiming: ConsumptionTiming.AtStart,
                Notes: null,
                SortIndex: null),
            CancellationToken.None);

        captured!.SortIndex.Should().Be(8, "max + 1");
    }

    [Fact]
    public async Task Honors_explicit_sort_index_when_provided()
    {
        var f = new Fixture();
        var (version, op) = BuildOp();
        Wire(f, version, op, maxSortIndex: 7);

        BomItem? captured = null;
        f.ChildRepo.Setup(r => r.AddBomItemAsync(It.IsAny<BomItem>(), It.IsAny<CancellationToken>()))
            .Callback<BomItem, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        await f.CreateSut().Handle(
            new AddBomItemRequest(
                op.Id, Guid.NewGuid(), null,
                Quantity: 1m,
                QuantityType: BomQuantityType.PerBatch,
                ScrapPercentage: null,
                IsOptional: false,
                PreferredWarehouseId: null,
                ConsumptionTiming: ConsumptionTiming.AtStart,
                Notes: null,
                SortIndex: 3),
            CancellationToken.None);

        captured!.SortIndex.Should().Be(3);
        f.ChildRepo.Verify(r => r.GetMaxBomItemSortIndexAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never, "explicit SortIndex must skip the Max query");
    }

    [Fact]
    public async Task Throws_NotFoundException_when_operation_missing()
    {
        var f = new Fixture();
        f.OpRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperationNode?)null);

        var act = () => f.CreateSut().Handle(
            new AddBomItemRequest(
                Guid.NewGuid(), Guid.NewGuid(), null,
                Quantity: 1m,
                QuantityType: BomQuantityType.PerUnit,
                ScrapPercentage: null,
                IsOptional: false,
                PreferredWarehouseId: null,
                ConsumptionTiming: ConsumptionTiming.AtStart,
                Notes: null,
                SortIndex: null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        f.ChildRepo.Verify(r => r.AddBomItemAsync(It.IsAny<BomItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rejects_addition_to_non_draft_version()
    {
        var f = new Fixture();
        var (version, op) = BuildOp(RecipeVersionStatus.Released);
        Wire(f, version, op);

        var act = () => f.CreateSut().Handle(
            new AddBomItemRequest(
                op.Id, Guid.NewGuid(), null,
                Quantity: 1m,
                QuantityType: BomQuantityType.PerUnit,
                ScrapPercentage: null,
                IsOptional: false,
                PreferredWarehouseId: null,
                ConsumptionTiming: ConsumptionTiming.AtStart,
                Notes: null,
                SortIndex: null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        f.ChildRepo.Verify(r => r.AddBomItemAsync(It.IsAny<BomItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
