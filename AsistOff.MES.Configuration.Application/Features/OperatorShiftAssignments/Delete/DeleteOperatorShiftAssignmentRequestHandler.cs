using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Delete;

internal sealed class DeleteOperatorShiftAssignmentRequestHandler(
    IOperatorShiftAssignmentsRepository repository)
    : IRequestHandler<DeleteOperatorShiftAssignmentRequest>
{
    public async Task Handle(DeleteOperatorShiftAssignmentRequest request, CancellationToken cancellationToken)
    {
        var assignment = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("OperatorShiftAssignment", request.Id);

        await repository.DeleteAsync(assignment.Id, cancellationToken);
    }
}
