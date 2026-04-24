using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OperationTemplates.Update;

internal sealed class UpdateOperationTemplateRequestHandler(
    IOperationTemplatesRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateOperationTemplateRequest>
{
    public async Task Handle(UpdateOperationTemplateRequest request, CancellationToken cancellationToken)
    {
        var template = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("OperationTemplate", request.Id);

        if (await repository.CodeExistsAsync(request.Code, request.Id, cancellationToken))
            throw new ConflictException($"Operation template with code '{request.Code}' already exists.");

        template.Code = request.Code;
        template.Name = request.Name;
        template.Description = request.Description;
        template.OperationType = request.OperationType;
        template.IsActive = request.IsActive;
        template.SetupTimeMinutes = request.SetupTimeMinutes;
        template.RunTimeMode = request.RunTimeMode;
        template.RunTimePerUnitSeconds = request.RunTimePerUnitSeconds;
        template.RunTimePerBatchMinutes = request.RunTimePerBatchMinutes;
        template.TeardownTimeMinutes = request.TeardownTimeMinutes;
        template.QueueTimeMinutes = request.QueueTimeMinutes;
        template.UpdatedAt = dateTimeProvider.UtcNow;

        await repository.UpdateAsync(template, cancellationToken);
    }
}
