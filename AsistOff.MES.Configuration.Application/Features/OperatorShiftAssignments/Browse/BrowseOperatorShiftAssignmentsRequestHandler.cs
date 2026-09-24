using AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Browse;

internal sealed class BrowseOperatorShiftAssignmentsRequestHandler(
    IOperatorShiftAssignmentsRepository repository)
    : IRequestHandler<BrowseOperatorShiftAssignmentsRequest, PagedResponse<OperatorShiftAssignmentResponse>>
{
    public async Task<PagedResponse<OperatorShiftAssignmentResponse>> Handle(
        BrowseOperatorShiftAssignmentsRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<OperatorShiftAssignment>(true);

        if (request.Date.HasValue)
            predicate = predicate.And(x => x.Date == request.Date.Value);
        if (request.DateFrom.HasValue)
            predicate = predicate.And(x => x.Date >= request.DateFrom.Value);
        if (request.DateTo.HasValue)
            predicate = predicate.And(x => x.Date <= request.DateTo.Value);
        if (request.OperatorId.HasValue)
            predicate = predicate.And(x => x.OperatorId == request.OperatorId.Value);
        if (request.ShiftId.HasValue)
            predicate = predicate.And(x => x.ShiftId == request.ShiftId.Value);

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<OperatorShiftAssignment>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        var result = items.Select(Map).ToList();
        return new PagedOperatorShiftAssignmentsResponse(result, totalCount, request.PageSize);
    }

    internal static OperatorShiftAssignmentResponse Map(OperatorShiftAssignment a) => new(
        a.Id,
        a.OperatorId,
        a.Operator?.Identifier,
        a.Operator is null ? null : $"{a.Operator.FirstName} {a.Operator.LastName}".Trim(),
        a.ShiftId,
        a.Shift?.Code,
        a.Shift?.Name,
        a.Date,
        a.Notes);
}
