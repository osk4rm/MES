using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Configuration.Application.Features.Operators.Delete;

internal sealed class DeleteOperatorRequestHandler(
    IOperatorsRepository operatorsRepository,
    ILogger<DeleteOperatorRequestHandler> logger)
    : IRequestHandler<DeleteOperatorRequest>
{
    public async Task Handle(DeleteOperatorRequest request, CancellationToken cancellationToken)
    {
        var operatorEntity = await operatorsRepository.GetByIdAsync(request.Id, cancellationToken);

        if (operatorEntity is null)
        {
            throw new NotFoundException("Operator", request.Id);
        }

        try
        {
            await operatorsRepository.DeleteAsync(request.Id, cancellationToken);
            logger.LogInformation("Deleted operator with ID {OperatorId}", request.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to delete operator with ID {OperatorId}", request.Id);
            throw;
        }
    }
}
