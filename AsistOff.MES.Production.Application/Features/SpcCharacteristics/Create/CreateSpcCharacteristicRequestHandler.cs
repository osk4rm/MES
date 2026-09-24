using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.SpcCharacteristics.Browse;
using AsistOff.MES.Production.Application.Features.SpcCharacteristics.Responses;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.SpcCharacteristics.Create;

internal sealed class CreateSpcCharacteristicRequestHandler(
    ISpcCharacteristicsRepository repository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateSpcCharacteristicRequest, SpcCharacteristicResponse>
{
    public async Task<SpcCharacteristicResponse> Handle(
        CreateSpcCharacteristicRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ValidationException(nameof(request.Code), "Code is required");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException(nameof(request.Name), "Name is required");

        SpcCharacteristicRules.ValidateLimits(
            request.ChartType,
            request.NominalValue,
            request.LowerSpecLimit,
            request.UpperSpecLimit,
            request.LowerControlLimit,
            request.UpperControlLimit,
            request.SampleSize);

        if (await repository.CodeExistsAsync(request.Code, null, cancellationToken))
            throw new ConflictException($"SPC characteristic with code '{request.Code}' already exists.");

        var characteristic = new SpcCharacteristic
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            ProductId = request.ProductId,
            MachineId = request.MachineId,
            ChartType = request.ChartType,
            NominalValue = request.NominalValue,
            LowerSpecLimit = request.LowerSpecLimit,
            UpperSpecLimit = request.UpperSpecLimit,
            LowerControlLimit = request.LowerControlLimit,
            UpperControlLimit = request.UpperControlLimit,
            SampleSize = request.SampleSize,
            Unit = request.Unit,
            IsActive = request.IsActive
        };
        await repository.AddAsync(characteristic, cancellationToken);
        return BrowseSpcCharacteristicsRequestHandler.Map(characteristic);
    }
}
