using ErrorOr;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Update;

internal sealed class UpdateWarehouseRequestHandler
    : IRequestHandler<UpdateWarehouseRequest, ErrorOr<Unit>>
{
    public Task<ErrorOr<Unit>> Handle(UpdateWarehouseRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}