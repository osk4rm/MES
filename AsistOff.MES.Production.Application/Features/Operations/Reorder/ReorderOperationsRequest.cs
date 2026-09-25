using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.Operations.Reorder;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record ReorderOperationsRequest(
    Guid VersionId,
    IReadOnlyCollection<OperationSortEntry> Order) : ITenantRequest;

public record OperationSortEntry(Guid OperationId, int SortIndex);
