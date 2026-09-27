using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Schedule;

/// <summary>
/// Read-only operator shift queue for one operator code: resolves the roster
/// assignment covering now, then composes Released/InProgress Production
/// Orders overlapping the shift window (same bounded server-side read as the
/// dispatch board) with the open Andon signals for the queued Work Centers.
/// An operator with no covering assignment gets an empty queue, not 404;
/// an unknown operator code yields 404 via <c>NotFoundException</c>.
/// </summary>
public record GetOperatorShiftQueueRequest(string OperatorCode, int? Take = null)
    : ITenantRequest<OperatorShiftQueueResponse>;
