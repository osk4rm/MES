using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Lots;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Lots.Create;

internal sealed class CreateLotRequestHandler(
    ILotsRepository repository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateLotRequest, LotResponse>
{
    public async Task<LotResponse> Handle(CreateLotRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ValidationException(nameof(request.Code), "Code is required.");

        if (request.Quantity < 0)
            throw new ValidationException(nameof(request.Quantity), "Quantity must be greater than or equal to 0.");

        if (await repository.CodeExistsAsync(request.Code, null, cancellationToken))
            throw new ConflictException($"Lot with code '{request.Code}' already exists.");

        var lot = new Lot
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            Code = request.Code,
            ProductId = request.ProductId,
            MeasureUnitId = request.MeasureUnitId,
            Quantity = request.Quantity,
            Status = LotStatus.Available,
            SupplierLotNumber = request.SupplierLotNumber,
            ProducedAt = request.ProducedAt,
            ExpiryDate = request.ExpiryDate,
            Notes = request.Notes,
            CreatedAt = dateTimeProvider.UtcNow
        };

        await repository.AddAsync(lot, cancellationToken);
        return LotMappings.Map(lot);
    }
}
