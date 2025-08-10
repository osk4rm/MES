using AsistOff.MES.Configuration.Application.Features.Operators.Responses;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Operators.Get;

internal sealed class GetOperatorRequestHandler(
    IOperatorsRepository operatorsRepository)
    : IRequestHandler<GetOperatorRequest, OperatorResponse>
{
    public async Task<OperatorResponse> Handle(GetOperatorRequest request, CancellationToken cancellationToken)
    {
        var operatorEntity = await operatorsRepository.GetByIdAsync(request.OperatorId, cancellationToken);

        if (operatorEntity is null)
        {
            throw new NotFoundException("Operator", request.OperatorId);
        }

        return new OperatorResponse
        {
            Id = operatorEntity!.Id,
            Identifier = operatorEntity.Identifier,
            FirstName = operatorEntity.FirstName,
            LastName = operatorEntity.LastName,
            RatePerHour = operatorEntity.RatePerHour,
            Department = operatorEntity.Department?.Name
        };
    }
}
