using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.SpcCharacteristics.Update;

internal sealed class UpdateSpcCharacteristicRequestHandler(ISpcCharacteristicsRepository repository)
    : IRequestHandler<UpdateSpcCharacteristicRequest>
{
    public async Task Handle(UpdateSpcCharacteristicRequest request, CancellationToken cancellationToken)
    {
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

        var characteristic = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("SpcCharacteristic", request.Id);

        // Code is immutable after create and is intentionally not part of this request.
        characteristic.Name = request.Name;
        characteristic.Description = request.Description;
        characteristic.ProductId = request.ProductId;
        characteristic.MachineId = request.MachineId;
        characteristic.ChartType = request.ChartType;
        characteristic.NominalValue = request.NominalValue;
        characteristic.LowerSpecLimit = request.LowerSpecLimit;
        characteristic.UpperSpecLimit = request.UpperSpecLimit;
        characteristic.LowerControlLimit = request.LowerControlLimit;
        characteristic.UpperControlLimit = request.UpperControlLimit;
        characteristic.SampleSize = request.SampleSize;
        characteristic.Unit = request.Unit;
        characteristic.IsActive = request.IsActive;

        await repository.UpdateAsync(characteristic, cancellationToken);
    }
}
