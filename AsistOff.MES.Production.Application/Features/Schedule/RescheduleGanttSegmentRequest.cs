using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.Schedule;

/// <summary>
/// Manual Gantt reschedule of one operation segment (issue #305, slice 2/3).
/// The route id is the <c>OperationNodeId</c>; the handler upserts the
/// <c>ScheduledOperation</c> override keyed by the unique
/// <c>(TenantId, ProductionOrderId, OperationNodeId)</c>, so the first move of
/// a computed bar creates the row and later moves update it. The parent
/// Production Order Xmin token guards against editing on top of a changed
/// order (stale token is a 409 with retry guidance).
/// </summary>
[RequirePermission(RbacDefaults.ProductionWrite)]
public record RescheduleGanttSegmentRequest(
    Guid OperationNodeId,
    Guid ProductionOrderId,
    DateTime PlannedStart,
    DateTime PlannedEnd,
    Guid MachineId,
    string? ConcurrencyToken,
    bool Force,
    string? Notes) : ITenantRequest<RescheduleGanttSegmentResponse>;
