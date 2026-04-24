using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Operations.Reorder;

public record ReorderOperationsRequest(
    Guid VersionId,
    IReadOnlyCollection<OperationSortEntry> Order) : ITenantRequest;

public record OperationSortEntry(Guid OperationId, int SortIndex);
