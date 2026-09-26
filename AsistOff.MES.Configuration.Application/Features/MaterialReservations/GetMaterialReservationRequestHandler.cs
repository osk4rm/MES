using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.MaterialReservations;

internal sealed class GetMaterialReservationRequestHandler(
    IMaterialReservationsRepository reservationsRepository)
    : IRequestHandler<GetMaterialReservationRequest, MaterialReservationResponse>
{
    public async Task<MaterialReservationResponse> Handle(
        GetMaterialReservationRequest request, CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
            throw new ValidationException(nameof(request.Id), "Reservation id is required.");

        var reservation = await reservationsRepository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("MaterialReservation", request.Id);

        return MaterialReservationResponse.Map(reservation);
    }
}
