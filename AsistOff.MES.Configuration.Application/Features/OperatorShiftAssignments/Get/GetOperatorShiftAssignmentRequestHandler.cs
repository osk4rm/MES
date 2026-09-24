using AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Browse;
using AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Responses;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Get;

internal sealed class GetOperatorShiftAssignmentRequestHandler(
    IOperatorShiftAssignmentsRepository repository)
    : IRequestHandler<GetOperatorShiftAssignmentRequest, OperatorShiftAssignmentResponse>
{
    public async Task<OperatorShiftAssignmentResponse> Handle(
        GetOperatorShiftAssignmentRequest request, CancellationToken cancellationToken)
    {
        var assignment = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("OperatorShiftAssignment", request.Id);

        return BrowseOperatorShiftAssignmentsRequestHandler.Map(assignment);
    }
}
