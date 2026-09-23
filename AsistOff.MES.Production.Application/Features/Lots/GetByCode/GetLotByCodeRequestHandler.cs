using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Lots.GetByCode;

internal sealed class GetLotByCodeRequestHandler(ILotsRepository repository)
    : IRequestHandler<GetLotByCodeRequest, LotResponse>
{
    public async Task<LotResponse> Handle(GetLotByCodeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ValidationException(nameof(request.Code), "Code is required.");

        var lot = await repository.GetByCodeAsync(request.Code, cancellationToken)
            ?? throw new NotFoundException("Lot", request.Code);

        return LotMappings.Map(lot);
    }
}
