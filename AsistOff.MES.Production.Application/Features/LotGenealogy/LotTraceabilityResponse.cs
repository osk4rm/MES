namespace AsistOff.MES.Production.Application.Features.LotGenealogy;

/// <summary>
/// One lot reached by transitive traceability traversal. Carries the edge
/// data that led to the lot (quantity, order, work center, operator,
/// timestamp) plus the lot identity (code, product) and the BFS depth
/// (1-based level from the root).
/// </summary>
public record LotTraceabilityNodeResponse(
    Guid LotId,
    string LotCode,
    Guid ProductId,
    int Depth,
    decimal ConsumedQuantity,
    Guid ProductionOrderId,
    string ProductionOrderCode,
    Guid MachineId,
    Guid? ReportedByOperatorId,
    DateTime OccurredAt);

/// <summary>
/// Transitive closure of lots reachable from a root lot, in BFS discovery
/// order. At most 500 nodes are returned; <see cref="Truncated"/> is set
/// when more nodes were reachable.
/// </summary>
public record LotTraceabilityResponse(
    Guid RootLotId,
    string RootLotCode,
    IReadOnlyCollection<LotTraceabilityNodeResponse> Nodes,
    bool Truncated);
