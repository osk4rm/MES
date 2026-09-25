using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.OperationDependencies.Set;

/// <summary>
/// Replaces the full dependency set of a single operation (inbound edges only).
/// Rejected if the resulting graph contains a cycle.
/// </summary>
[RequirePermission(RbacDefaults.ProductionWrite)]
public record SetOperationDependenciesRequest(
    Guid OperationId,
    IReadOnlyCollection<DependencyEntry> Dependencies) : ITenantRequest;

public record DependencyEntry(
    Guid PredecessorOperationId,
    OperationDependencyType DependencyType,
    decimal? LagMinutes);
