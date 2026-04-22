using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Configuration.Application.Features.Operators.Update;

internal sealed class UpdateOperatorRequestHandler(
    IOperatorsRepository operatorsRepository,
    ILogger<UpdateOperatorRequestHandler> logger)
    : IRequestHandler<UpdateOperatorRequest>
{
    public async Task Handle(UpdateOperatorRequest request, CancellationToken cancellationToken)
    {
        var operatorEntity = await operatorsRepository.GetByIdAsync(request.Id, cancellationToken);

        if (operatorEntity is null)
        {
            throw new NotFoundException("Operator", request.Id);
        }
        
        operatorEntity!.Identifier = request.Identifier;
        operatorEntity.FirstName = request.FirstName;
        operatorEntity.LastName = request.LastName;
        operatorEntity.RatePerHour = request.RatePerHour;
        operatorEntity.DepartmentId = request.DepartmentId;
        operatorEntity.UserId = request.UserId;

        try
        {
            await operatorsRepository.UpdateAsync(operatorEntity, cancellationToken);
            logger.LogInformation("Updated operator with ID {OperatorId}", operatorEntity.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to update operator with ID {OperatorId}", request.Id);
            throw;
        }
    }
}
