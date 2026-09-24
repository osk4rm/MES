using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Responses;

public record OperatorShiftAssignmentResponse(
    Guid Id,
    Guid OperatorId,
    string? OperatorIdentifier,
    string? OperatorName,
    Guid ShiftId,
    string? ShiftCode,
    string? ShiftName,
    DateOnly Date,
    string? Notes);

public class PagedOperatorShiftAssignmentsResponse(
    IReadOnlyCollection<OperatorShiftAssignmentResponse> items,
    int totalCount,
    int? pageSize)
    : PagedResponse<OperatorShiftAssignmentResponse>(items, totalCount, pageSize);
