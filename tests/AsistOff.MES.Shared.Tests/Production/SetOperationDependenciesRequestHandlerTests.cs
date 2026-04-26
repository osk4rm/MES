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
            new HashSet<Guid>(), Array.Empty<DependencyEdge>());
        result.Should().BeFalse();
    }

    [Fact]
    public void HasCycle_returns_false_for_linear_chain()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var edges = new[] { new DependencyEdge(b, a), new DependencyEdge(c, b) }; // a -> b -> c

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
        var edges = new[]
        {
            new DependencyEdge(b, a), new DependencyEdge(c, a),
            new DependencyEdge(d, b), new DependencyEdge(d, c)
        };

        var result = SetOperationDependenciesRequestHandler.HasCycle(
            new HashSet<Guid> { a, b, c, d }, edges);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasCycle_detects_direct_two_node_cycle()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        // a -> b -> a
        var edges = new[] { new DependencyEdge(b, a), new DependencyEdge(a, b) };

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
        var edges = new[]
        {
            new DependencyEdge(b, a), new DependencyEdge(c, b),
            new DependencyEdge(d, c), new DependencyEdge(b, d)
        };

        var result = SetOperationDependenciesRequestHandler.HasCycle(
            new HashSet<Guid> { a, b, c, d }, edges);

        result.Should().BeTrue();
    }

    // ----- Handler tests -----
    //
    // Handler under test: SetOperationDependenciesRequestHandler.
    // It now operates on IOperationDependenciesRepository directly (no longer loads
    // OperationNode with eager Includes), which avoids the DbUpdateConcurrencyException
    // that arose when mutating the navigation collection of a tracked principal under
    // EF's split-query loading + (OperationNodeId, PredecessorOperationNodeId) unique index.

    private static readonly Guid TenantId = Guid.NewGuid();

    private sealed class HandlerFixture
    {
        public Mock<IOperationNodesRepository> Operations { get; } = new();
        public Mock<IOperationDependenciesRepository> Dependencies { get; } = new();
        public Mock<IRecipeVersionsRepository> Versions { get; } = new();
        public Mock<IGuidProvider> Guids { get; } = new();
        public Mock<ITenantContext> Tenant { get; } = new();

        // In-memory edge store standing in for the DbSet<OperationDependency>.
        public List<OperationDependency> EdgeStore { get; } = new();
        public int SaveChangesCalls { get; private set; }

        public HandlerFixture()
        {
            Guids.Setup(g => g.NewGuid()).Returns(Guid.NewGuid);
            Tenant.SetupGet(t => t.TenantId).Returns(TenantId);

            Dependencies.Setup(r => r.Add(It.IsAny<OperationDependency>()))
                .Callback<OperationDependency>(e => EdgeStore.Add(e));
            Dependencies.Setup(r => r.Remove(It.IsAny<OperationDependency>()))
                .Callback<OperationDependency>(e => EdgeStore.Remove(e));
            Dependencies.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback(() => SaveChangesCalls++)
                .Returns(Task.CompletedTask);
        }

        public SetOperationDependenciesRequestHandler CreateSut() =>
            new(Operations.Object, Dependencies.Object, Versions.Object, Guids.Object, Tenant.Object);
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

    /// <summary>
    /// Wires the repositories so that:
    /// * GetAsync(target.Id) returns <paramref name="target"/>
    /// * the recipe version is the (Draft) <paramref name="version"/>
    /// * the version's operation Id list is taken from <paramref name="versionOps"/>
    /// * existing edges and global edge projections come from the fixture's <c>EdgeStore</c>.
    /// </summary>
    private static void Wire(HandlerFixture f, RecipeVersion version, OperationNode target, params OperationNode[] versionOps)
    {
        f.Operations.Setup(r => r.GetAsync(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        f.Versions.Setup(r => r.GetAsync(version.Id, It.IsAny<CancellationToken>())).ReturnsAsync(version);
        f.Dependencies.Setup(r => r.ListOperationIdsForVersionAsync(version.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(versionOps.Select(o => o.Id).ToArray());

        // Edge projections come from the fixture's mutable edge store, scoped per-call
        // so that "ListForOperation" only returns edges for the targeted operation.
        f.Dependencies.Setup(r => r.ListEdgesForVersionAsync(version.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => f.EdgeStore
                .Select(e => new DependencyEdge(e.OperationNodeId, e.PredecessorOperationNodeId))
                .ToArray());
        f.Dependencies.Setup(r => r.ListForOperationAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => f.EdgeStore.Where(e => e.OperationNodeId == target.Id).ToArray());
    }

    [Fact]
    public async Task Replacing_with_identical_set_keeps_existing_edges_and_does_not_recreate_them()
    {
        // Repro for the original DbUpdateConcurrencyException: PUT submits the same
        // predecessor that already exists. The diff is a no-op for unchanged edges:
        // no Add, no Remove, and the existing edge instance is preserved.
        var f = new HandlerFixture();
        var (version, op1, op2, _) = BuildVersion();
        var existingEdgeId = Guid.NewGuid();
        f.EdgeStore.Add(new OperationDependency
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

        f.EdgeStore.Should().HaveCount(1);
        f.EdgeStore.Single().Id.Should().Be(existingEdgeId, "the edge must be reused, not deleted+recreated");
        f.Guids.Verify(g => g.NewGuid(), Times.Never, "no new edge should have been allocated");
        f.Dependencies.Verify(r => r.Add(It.IsAny<OperationDependency>()), Times.Never);
        f.Dependencies.Verify(r => r.Remove(It.IsAny<OperationDependency>()), Times.Never);
        f.SaveChangesCalls.Should().Be(1);
    }

    [Fact]
    public async Task Updates_lag_and_type_in_place_without_changing_edge_id()
    {
        var f = new HandlerFixture();
        var (version, op1, op2, _) = BuildVersion();
        var edgeId = Guid.NewGuid();
        f.EdgeStore.Add(new OperationDependency
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

        var edge = f.EdgeStore.Single();
        edge.Id.Should().Be(edgeId);
        edge.DependencyType.Should().Be(OperationDependencyType.StartToStart);
        edge.LagMinutes.Should().Be(12m);
        f.Guids.Verify(g => g.NewGuid(), Times.Never);
        f.Dependencies.Verify(r => r.Add(It.IsAny<OperationDependency>()), Times.Never);
        f.Dependencies.Verify(r => r.Remove(It.IsAny<OperationDependency>()), Times.Never);
    }

    [Fact]
    public async Task Removes_only_predecessors_no_longer_requested()
    {
        var f = new HandlerFixture();
        var (version, op1, op2, op3) = BuildVersion();
        // op3 currently depends on both op1 and op2.
        var dropEdge = new OperationDependency { Id = Guid.NewGuid(), TenantId = TenantId, RecipeVersionId = version.Id, OperationNodeId = op3.Id, PredecessorOperationNodeId = op1.Id };
        var keepEdgeId = Guid.NewGuid();
        var keepEdge = new OperationDependency { Id = keepEdgeId, TenantId = TenantId, RecipeVersionId = version.Id, OperationNodeId = op3.Id, PredecessorOperationNodeId = op2.Id, DependencyType = OperationDependencyType.FinishToStart };
        f.EdgeStore.Add(dropEdge);
        f.EdgeStore.Add(keepEdge);
        Wire(f, version, op3, op1, op2, op3);

        await f.CreateSut().Handle(
            new SetOperationDependenciesRequest(op3.Id, new[]
            {
                new DependencyEntry(op2.Id, OperationDependencyType.FinishToStart, null)
            }),
            CancellationToken.None);

        f.EdgeStore.Should().HaveCount(1);
        f.EdgeStore.Single().Id.Should().Be(keepEdgeId);
        f.EdgeStore.Single().PredecessorOperationNodeId.Should().Be(op2.Id);
        f.Dependencies.Verify(r => r.Remove(dropEdge), Times.Once);
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

        var edge = f.EdgeStore.Single();
        edge.Id.Should().Be(allocatedId);
        edge.TenantId.Should().Be(TenantId);
        edge.OperationNodeId.Should().Be(op2.Id);
        edge.PredecessorOperationNodeId.Should().Be(op1.Id);
        edge.RecipeVersionId.Should().Be(version.Id);
    }

    [Fact]
    public async Task Detects_cycle_through_sibling_operation_edges()
    {
        // op1 -> op2 -> op3 already; submitting "op3 must precede op1" closes the loop.
        // The version-wide edge projection (ListEdgesForVersionAsync) makes sibling edges
        // visible to the cycle guard.
        var f = new HandlerFixture();
        var (version, op1, op2, op3) = BuildVersion();
        f.EdgeStore.Add(new OperationDependency { Id = Guid.NewGuid(), TenantId = TenantId, RecipeVersionId = version.Id, OperationNodeId = op2.Id, PredecessorOperationNodeId = op1.Id });
        f.EdgeStore.Add(new OperationDependency { Id = Guid.NewGuid(), TenantId = TenantId, RecipeVersionId = version.Id, OperationNodeId = op3.Id, PredecessorOperationNodeId = op2.Id });
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
        f.Operations.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
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
        f.EdgeStore.Add(new OperationDependency { Id = Guid.NewGuid(), TenantId = TenantId, RecipeVersionId = version.Id, OperationNodeId = op2.Id, PredecessorOperationNodeId = op1.Id });
        Wire(f, version, op2, op1, op2);

        await f.CreateSut().Handle(
            new SetOperationDependenciesRequest(op2.Id, Array.Empty<DependencyEntry>()),
            CancellationToken.None);

        f.EdgeStore.Should().BeEmpty();
        f.SaveChangesCalls.Should().Be(1);
    }
}
