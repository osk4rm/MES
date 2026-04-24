using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.OperationDependencies.Set;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class SetOperationDependenciesRequestHandlerTests
{
    [Fact]
    public void HasCycle_returns_false_for_empty_graph()
    {
        var result = SetOperationDependenciesRequestHandler.HasCycle(
            new HashSet<Guid>(), Array.Empty<(Guid, Guid)>());
        result.Should().BeFalse();
    }

    [Fact]
    public void HasCycle_returns_false_for_linear_chain()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var edges = new[] { (b, a), (c, b) }; // a -> b -> c

        var result = SetOperationDependenciesRequestHandler.HasCycle(
            new HashSet<Guid> { a, b, c }, edges);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasCycle_returns_false_for_diamond_DAG()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var c = Guid.NewGuid(); var d = Guid.NewGuid();
        // a -> b, a -> c, b -> d, c -> d
        var edges = new[] { (b, a), (c, a), (d, b), (d, c) };

        var result = SetOperationDependenciesRequestHandler.HasCycle(
            new HashSet<Guid> { a, b, c, d }, edges);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasCycle_detects_direct_two_node_cycle()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        // a -> b -> a
        var edges = new[] { (b, a), (a, b) };

        var result = SetOperationDependenciesRequestHandler.HasCycle(
            new HashSet<Guid> { a, b }, edges);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasCycle_detects_longer_cycle()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var c = Guid.NewGuid(); var d = Guid.NewGuid();
        // a -> b -> c -> d -> b (cycle b->c->d->b)
        var edges = new[] { (b, a), (c, b), (d, c), (b, d) };

        var result = SetOperationDependenciesRequestHandler.HasCycle(
            new HashSet<Guid> { a, b, c, d }, edges);

        result.Should().BeTrue();
    }

    // ----- Handler tests -----
    //
    // These tests exercise the diff logic that replaced the previous
    // Clear() + Add() rewrite (which produced DbUpdateConcurrencyException
    // under EF's batching and the (OperationNodeId, PredecessorOperationNodeId)
    // unique index when the request re-submitted an unchanged set).

    private static readonly Guid TenantId = Guid.NewGuid();

    private sealed class HandlerFixture
    {
        public Mock<IOperationNodesRepository> Operations { get; } = new();
        public Mock<IRecipeVersionsRepository> Versions { get; } = new();
        public Mock<IGuidProvider> Guids { get; } = new();
        public Mock<ITenantContext> Tenant { get; } = new();
        public int SaveChangesCalls { get; private set; }

        public HandlerFixture()
        {
            Guids.Setup(g => g.NewGuid()).Returns(Guid.NewGuid);
            Tenant.SetupGet(t => t.TenantId).Returns(TenantId);
            Operations.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback(() => SaveChangesCalls++)
                .Returns(Task.CompletedTask);
        }

        public SetOperationDependenciesRequestHandler CreateSut() =>
            new(Operations.Object, Versions.Object, Guids.Object, Tenant.Object);
    }

    private static (RecipeVersion version, OperationNode op1, OperationNode op2, OperationNode op3) BuildVersion()
    {
        var versionId = Guid.NewGuid();
        var version = new RecipeVersion
        {
            Id = versionId,
            RecipeId = Guid.NewGuid(),
            VersionNumber = 1,
            Status = RecipeVersionStatus.Draft,
            TenantId = TenantId
        };
        var op1 = new OperationNode { Id = Guid.NewGuid(), RecipeVersionId = versionId, TenantId = TenantId, Code = "OP1", Name = "Setup", SortIndex = 0 };
        var op2 = new OperationNode { Id = Guid.NewGuid(), RecipeVersionId = versionId, TenantId = TenantId, Code = "OP2", Name = "Run", SortIndex = 1 };
        var op3 = new OperationNode { Id = Guid.NewGuid(), RecipeVersionId = versionId, TenantId = TenantId, Code = "OP3", Name = "Pack", SortIndex = 2 };
        return (version, op1, op2, op3);
    }

    private static void Wire(HandlerFixture f, RecipeVersion version, OperationNode target, params OperationNode[] versionOps)
    {
        f.Operations.Setup(r => r.GetWithDetailsAsync(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        f.Versions.Setup(r => r.GetAsync(version.Id, It.IsAny<CancellationToken>())).ReturnsAsync(version);
        f.Operations.Setup(r => r.ListForVersionWithDependenciesAsync(version.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(versionOps);
    }

    [Fact]
    public async Task Replacing_with_identical_set_keeps_existing_edges_and_does_not_recreate_them()
    {
        // Repro for the original DbUpdateConcurrencyException: PUT submits the
        // same predecessor that already exists. The previous Clear()+Add()
        // version produced DELETE+INSERT batches; the new diff is a no-op
        // for unchanged edges.
        var f = new HandlerFixture();
        var (version, op1, op2, _) = BuildVersion();
        var existingEdgeId = Guid.NewGuid();
        op2.Dependencies.Add(new OperationDependency
        {
            Id = existingEdgeId,
            TenantId = TenantId,
            RecipeVersionId = version.Id,
            OperationNodeId = op2.Id,
            PredecessorOperationNodeId = op1.Id,
            DependencyType = OperationDependencyType.FinishToStart,
            LagMinutes = null
        });
        Wire(f, version, op2, op1, op2);

        await f.CreateSut().Handle(
            new SetOperationDependenciesRequest(op2.Id, new[]
            {
                new DependencyEntry(op1.Id, OperationDependencyType.FinishToStart, null)
            }),
            CancellationToken.None);

        op2.Dependencies.Should().HaveCount(1);
        op2.Dependencies.Single().Id.Should().Be(existingEdgeId, "the edge must be reused, not deleted+recreated");
        f.Guids.Verify(g => g.NewGuid(), Times.Never, "no new edge should have been allocated");
        f.SaveChangesCalls.Should().Be(1);
    }

    [Fact]
    public async Task Updates_lag_and_type_in_place_without_changing_edge_id()
    {
        var f = new HandlerFixture();
        var (version, op1, op2, _) = BuildVersion();
        var edgeId = Guid.NewGuid();
        op2.Dependencies.Add(new OperationDependency
        {
            Id = edgeId,
            TenantId = TenantId,
            RecipeVersionId = version.Id,
            OperationNodeId = op2.Id,
            PredecessorOperationNodeId = op1.Id,
            DependencyType = OperationDependencyType.FinishToStart,
            LagMinutes = 5m
        });
        Wire(f, version, op2, op1, op2);

        await f.CreateSut().Handle(
            new SetOperationDependenciesRequest(op2.Id, new[]
            {
                new DependencyEntry(op1.Id, OperationDependencyType.StartToStart, 12m)
            }),
            CancellationToken.None);

        var edge = op2.Dependencies.Single();
        edge.Id.Should().Be(edgeId);
        edge.DependencyType.Should().Be(OperationDependencyType.StartToStart);
        edge.LagMinutes.Should().Be(12m);
        f.Guids.Verify(g => g.NewGuid(), Times.Never);
    }

    [Fact]
    public async Task Removes_only_predecessors_no_longer_requested()
    {
        var f = new HandlerFixture();
        var (version, op1, op2, op3) = BuildVersion();
        // op3 currently depends on both op1 and op2.
        op3.Dependencies.Add(new OperationDependency { Id = Guid.NewGuid(), TenantId = TenantId, RecipeVersionId = version.Id, OperationNodeId = op3.Id, PredecessorOperationNodeId = op1.Id });
        var keepEdgeId = Guid.NewGuid();
        op3.Dependencies.Add(new OperationDependency { Id = keepEdgeId, TenantId = TenantId, RecipeVersionId = version.Id, OperationNodeId = op3.Id, PredecessorOperationNodeId = op2.Id, DependencyType = OperationDependencyType.FinishToStart });
        Wire(f, version, op3, op1, op2, op3);

        await f.CreateSut().Handle(
            new SetOperationDependenciesRequest(op3.Id, new[]
            {
                new DependencyEntry(op2.Id, OperationDependencyType.FinishToStart, null)
            }),
            CancellationToken.None);

        op3.Dependencies.Should().HaveCount(1);
        op3.Dependencies.Single().Id.Should().Be(keepEdgeId);
        op3.Dependencies.Single().PredecessorOperationNodeId.Should().Be(op2.Id);
    }

    [Fact]
    public async Task Adds_new_predecessor_with_fresh_id_and_tenant()
    {
        var f = new HandlerFixture();
        var (version, op1, op2, _) = BuildVersion();
        Wire(f, version, op2, op1, op2);

        var allocatedId = Guid.NewGuid();
        f.Guids.Setup(g => g.NewGuid()).Returns(allocatedId);

        await f.CreateSut().Handle(
            new SetOperationDependenciesRequest(op2.Id, new[]
            {
                new DependencyEntry(op1.Id, OperationDependencyType.FinishToStart, null)
            }),
            CancellationToken.None);

        var edge = op2.Dependencies.Single();
        edge.Id.Should().Be(allocatedId);
        edge.TenantId.Should().Be(TenantId);
        edge.OperationNodeId.Should().Be(op2.Id);
        edge.PredecessorOperationNodeId.Should().Be(op1.Id);
        edge.RecipeVersionId.Should().Be(version.Id);
    }

    [Fact]
    public async Task Detects_cycle_through_sibling_operation_edges()
    {
        // op1 -> op2 -> op3 already; submitting "op3 must precede op1" closes
        // the loop. The bug before the fix: ListForVersionAsync did not Include
        // Dependencies, so sibling edges were invisible and the cycle slipped
        // through the guard.
        var f = new HandlerFixture();
        var (version, op1, op2, op3) = BuildVersion();
        op2.Dependencies.Add(new OperationDependency { Id = Guid.NewGuid(), TenantId = TenantId, RecipeVersionId = version.Id, OperationNodeId = op2.Id, PredecessorOperationNodeId = op1.Id });
        op3.Dependencies.Add(new OperationDependency { Id = Guid.NewGuid(), TenantId = TenantId, RecipeVersionId = version.Id, OperationNodeId = op3.Id, PredecessorOperationNodeId = op2.Id });
        Wire(f, version, op1, op1, op2, op3);

        var act = () => f.CreateSut().Handle(
            new SetOperationDependenciesRequest(op1.Id, new[]
            {
                new DependencyEntry(op3.Id, OperationDependencyType.FinishToStart, null)
            }),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*cycle*");
        f.SaveChangesCalls.Should().Be(0);
    }

    [Fact]
    public async Task Rejects_self_dependency()
    {
        var f = new HandlerFixture();
        var (version, op1, op2, _) = BuildVersion();
        Wire(f, version, op2, op1, op2);

        var act = () => f.CreateSut().Handle(
            new SetOperationDependenciesRequest(op2.Id, new[]
            {
                new DependencyEntry(op2.Id, OperationDependencyType.FinishToStart, null)
            }),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*itself*");
    }

    [Fact]
    public async Task Rejects_duplicate_predecessors_in_request()
    {
        var f = new HandlerFixture();
        var (version, op1, op2, _) = BuildVersion();
        Wire(f, version, op2, op1, op2);

        var act = () => f.CreateSut().Handle(
            new SetOperationDependenciesRequest(op2.Id, new[]
            {
                new DependencyEntry(op1.Id, OperationDependencyType.FinishToStart, null),
                new DependencyEntry(op1.Id, OperationDependencyType.StartToStart, 10m)
            }),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Duplicate*");
    }

    [Fact]
    public async Task Rejects_predecessor_not_in_version()
    {
        var f = new HandlerFixture();
        var (version, op1, op2, _) = BuildVersion();
        Wire(f, version, op2, op1, op2);

        var act = () => f.CreateSut().Handle(
            new SetOperationDependenciesRequest(op2.Id, new[]
            {
                new DependencyEntry(Guid.NewGuid(), OperationDependencyType.FinishToStart, null)
            }),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*does not belong*");
    }

    [Fact]
    public async Task Throws_NotFoundException_when_operation_missing()
    {
        var f = new HandlerFixture();
        f.Operations.Setup(r => r.GetWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperationNode?)null);

        var act = () => f.CreateSut().Handle(
            new SetOperationDependenciesRequest(Guid.NewGuid(), Array.Empty<DependencyEntry>()),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Rejects_modification_of_non_draft_version()
    {
        var f = new HandlerFixture();
        var (version, op1, op2, _) = BuildVersion();
        version.Status = RecipeVersionStatus.Released;
        Wire(f, version, op2, op1, op2);

        var act = () => f.CreateSut().Handle(
            new SetOperationDependenciesRequest(op2.Id, Array.Empty<DependencyEntry>()),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Empty_dependencies_clears_all_existing_edges()
    {
        var f = new HandlerFixture();
        var (version, op1, op2, _) = BuildVersion();
        op2.Dependencies.Add(new OperationDependency { Id = Guid.NewGuid(), TenantId = TenantId, RecipeVersionId = version.Id, OperationNodeId = op2.Id, PredecessorOperationNodeId = op1.Id });
        Wire(f, version, op2, op1, op2);

        await f.CreateSut().Handle(
            new SetOperationDependenciesRequest(op2.Id, Array.Empty<DependencyEntry>()),
            CancellationToken.None);

        op2.Dependencies.Should().BeEmpty();
        f.SaveChangesCalls.Should().Be(1);
    }
}

