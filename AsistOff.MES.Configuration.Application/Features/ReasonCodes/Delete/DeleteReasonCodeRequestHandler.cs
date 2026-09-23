using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.ReasonCodes.Delete;

internal sealed class DeleteReasonCodeRequestHandler(IReasonCodesRepository repository)
    : IRequestHandler<DeleteReasonCodeRequest>
{
    public async Task Handle(DeleteReasonCodeRequest request, CancellationToken cancellationToken)
    {
        var reasonCode = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ReasonCode", request.Id);

        await repository.DeleteAsync(reasonCode.Id, cancellationToken);
    }
}
