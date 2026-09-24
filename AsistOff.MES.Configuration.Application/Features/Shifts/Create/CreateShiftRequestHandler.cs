using AsistOff.MES.Configuration.Application.Features.Shifts.Browse;
using AsistOff.MES.Configuration.Application.Features.Shifts.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Shifts.Create;

internal sealed class CreateShiftRequestHandler(
    IShiftsRepository repository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateShiftRequest, ShiftResponse>
{
    public async Task<ShiftResponse> Handle(
        CreateShiftRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ValidationException(nameof(request.Code), "Code is required");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException(nameof(request.Name), "Name is required");

        if (await repository.CodeExistsAsync(request.Code, null, cancellationToken))
            throw new ConflictException($"Shift with code '{request.Code}' already exists.");

        var shift = new Shift
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IsActive = request.IsActive
        };

        await repository.AddAsync(shift, cancellationToken);
        return BrowseShiftsRequestHandler.Map(shift);
    }
}
