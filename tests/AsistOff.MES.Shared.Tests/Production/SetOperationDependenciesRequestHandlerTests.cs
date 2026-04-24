using AsistOff.MES.Production.Application.Features.OperationDependencies.Set;
using FluentAssertions;

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
}
