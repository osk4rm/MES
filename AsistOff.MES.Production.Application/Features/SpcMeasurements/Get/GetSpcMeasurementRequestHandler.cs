using AsistOff.MES.Production.Application.Features.SpcMeasurements.Browse;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.SpcMeasurements.Get;

internal sealed class GetSpcMeasurementRequestHandler(ISpcMeasurementsRepository repository)
    : IRequestHandler<GetSpcMeasurementRequest, SpcMeasurementResponse>
{
    public async Task<SpcMeasurementResponse> Handle(GetSpcMeasurementRequest request, CancellationToken cancellationToken)
    {
        var measurement = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("SpcMeasurement", request.Id);

        return BrowseSpcMeasurementsRequestHandler.Map(measurement);
    }
}
