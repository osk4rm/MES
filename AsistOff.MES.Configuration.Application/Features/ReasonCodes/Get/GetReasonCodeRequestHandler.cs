using AsistOff.MES.Configuration.Application.Features.ReasonCodes.Browse;
using AsistOff.MES.Configuration.Application.Features.ReasonCodes.Responses;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.ReasonCodes.Get;

internal sealed class GetReasonCodeRequestHandler(IReasonCodesRepository repository)
    : IRequestHandler<GetReasonCodeRequest, ReasonCodeResponse>
{
    public async Task<ReasonCodeResponse> Handle(
        GetReasonCodeRequest request, CancellationToken cancellationToken)
    {
        var reasonCode = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ReasonCode", request.Id);

        return BrowseReasonCodesRequestHandler.Map(reasonCode);
    }
}
