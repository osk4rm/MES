using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Lots.Update;

internal sealed class UpdateLotRequestHandler(
    ILotsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateLotRequest>
{
    public async Task Handle(UpdateLotRequest request, CancellationToken cancellationToken)
    {
        var lot = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Lot", request.Id);

        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ValidationException(nameof(request.Code), "Code is required.");

        if (request.Code.Length > 50)
            throw new ValidationException(nameof(request.Code), "Code must not exceed 50 characters.");

        if (request.SupplierLotNumber is { Length: > 100 })
            throw new ValidationException(nameof(request.SupplierLotNumber), "Supplier lot number must not exceed 100 characters.");

        if (request.Notes is { Length: > 1000 })
            throw new ValidationException(nameof(request.Notes), "Notes must not exceed 1000 characters.");

        if (request.Quantity < 0)
            throw new ValidationException(nameof(request.Quantity), "Quantity must be greater than or equal to 0.");

        if (await repository.CodeExistsAsync(request.Code, request.Id, cancellationToken))
            throw new ConflictException($"Lot with code '{request.Code}' already exists.");

        lot.Code = request.Code;
        lot.ProductId = request.ProductId;
        lot.MeasureUnitId = request.MeasureUnitId;
        lot.Quantity = request.Quantity;
        lot.SupplierLotNumber = request.SupplierLotNumber;
        lot.ProducedAt = request.ProducedAt;
        lot.ExpiryDate = request.ExpiryDate;
        lot.Notes = request.Notes;
        lot.UpdatedAt = dateTimeProvider.UtcNow;

        await repository.UpdateAsync(lot, cancellationToken);
    }
}
