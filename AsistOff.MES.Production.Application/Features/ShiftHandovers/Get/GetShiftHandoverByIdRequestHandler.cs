using AsistOff.MES.Production.Application.Features.ShiftHandovers.Browse;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ShiftHandovers.Get;

internal sealed class GetShiftHandoverByIdRequestHandler(IShiftHandoversRepository repository)
    : IRequestHandler<GetShiftHandoverByIdRequest, ShiftHandoverResponse>
{
    public async Task<ShiftHandoverResponse> Handle(GetShiftHandoverByIdRequest request, CancellationToken cancellationToken)
    {
        // The global tenant query filter scopes the lookup to the caller
        // tenant, so unknown and cross-tenant ids both yield 404.
        var handover = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ShiftHandover", request.Id);

        return BrowseShiftHandoversRequestHandler.Map(handover, handover.ShiftId is null);
    }
}
