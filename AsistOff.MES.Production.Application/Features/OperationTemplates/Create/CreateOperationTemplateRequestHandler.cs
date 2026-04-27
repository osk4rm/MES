using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Production.Application.Features.OperationTemplates.Browse;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OperationTemplates.Create;

internal sealed class CreateOperationTemplateRequestHandler(
    IOperationTemplatesRepository repository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateOperationTemplateRequest, OperationTemplateResponse>
{
    public async Task<OperationTemplateResponse> Handle(CreateOperationTemplateRequest request, CancellationToken cancellationToken)
    {
        if (await repository.CodeExistsAsync(request.Code, null, cancellationToken))
            throw new ConflictException($"Operation template with code '{request.Code}' already exists.");

        var template = new OperationTemplate
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            OperationType = request.OperationType,
            IsActive = request.IsActive,
            SetupTimeMinutes = request.SetupTimeMinutes,
            RunTimeMode = request.RunTimeMode,
            RunTimePerUnitSeconds = request.RunTimePerUnitSeconds,
            RunTimePerBatchMinutes = request.RunTimePerBatchMinutes,
            TeardownTimeMinutes = request.TeardownTimeMinutes,
            QueueTimeMinutes = request.QueueTimeMinutes,
            CreatedAt = dateTimeProvider.UtcNow
        };

        await repository.AddAsync(template, cancellationToken);
        return BrowseOperationTemplatesRequestHandler.Map(template);
    }
}
