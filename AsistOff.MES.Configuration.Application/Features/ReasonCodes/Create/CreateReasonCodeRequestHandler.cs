using AsistOff.MES.Configuration.Application.Features.ReasonCodes.Browse;
using AsistOff.MES.Configuration.Application.Features.ReasonCodes.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.ReasonCodes.Create;

internal sealed class CreateReasonCodeRequestHandler(
    IReasonCodesRepository repository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateReasonCodeRequest, ReasonCodeResponse>
{
    public async Task<ReasonCodeResponse> Handle(
        CreateReasonCodeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ValidationException(nameof(request.Code), "Code is required");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException(nameof(request.Name), "Name is required");

        if (await repository.CodeExistsAsync(request.Code, null, cancellationToken))
            throw new ConflictException($"Reason code with code '{request.Code}' already exists.");

        var reasonCode = new ReasonCode
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            Category = request.Category,
            IsActive = request.IsActive,
            SortIndex = request.SortIndex
        };
        await repository.AddAsync(reasonCode, cancellationToken);
        return BrowseReasonCodesRequestHandler.Map(reasonCode);
    }
}
