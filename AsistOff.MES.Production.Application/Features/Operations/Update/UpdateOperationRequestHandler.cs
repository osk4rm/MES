using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Operations.Update;

internal sealed class UpdateOperationRequestHandler(
    IOperationNodesRepository operationsRepository,
    IRecipeVersionsRepository versionsRepository)
    : IRequestHandler<UpdateOperationRequest>
{
    public async Task Handle(UpdateOperationRequest request, CancellationToken cancellationToken)
    {
        var op = await operationsRepository.GetAsync(request.OperationId, cancellationToken)
            ?? throw new NotFoundException("OperationNode", request.OperationId);

        await VersionGuard.EnsureDraftAsync(versionsRepository, op.RecipeVersionId, cancellationToken);

        op.Code = request.Code;
        op.Name = request.Name;
        op.Description = request.Description;
        op.OperationType = request.OperationType;
        op.SortIndex = request.SortIndex;
        op.SetupTimeMinutes = request.SetupTimeMinutes;
        op.RunTimeMode = request.RunTimeMode;
        op.RunTimePerUnitSeconds = request.RunTimePerUnitSeconds;
        op.RunTimePerBatchMinutes = request.RunTimePerBatchMinutes;
        op.TeardownTimeMinutes = request.TeardownTimeMinutes;
        op.QueueTimeMinutes = request.QueueTimeMinutes;
        op.IsOptional = request.IsOptional;
        op.AllowParallelExecution = request.AllowParallelExecution;
        op.ExpectedQuantity = request.ExpectedQuantity;

        await operationsRepository.UpdateAsync(op, cancellationToken);
    }
}
