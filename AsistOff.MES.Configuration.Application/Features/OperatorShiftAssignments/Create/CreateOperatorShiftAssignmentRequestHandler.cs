using AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Browse;
using AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Create;

internal sealed class CreateOperatorShiftAssignmentRequestHandler(
    IOperatorShiftAssignmentsRepository repository,
    IOperatorsRepository operatorsRepository,
    IShiftsRepository shiftsRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateOperatorShiftAssignmentRequest, OperatorShiftAssignmentResponse>
{
    public async Task<OperatorShiftAssignmentResponse> Handle(
        CreateOperatorShiftAssignmentRequest request, CancellationToken cancellationToken)
    {
        if (request.OperatorId == Guid.Empty)
            throw new ValidationException(nameof(request.OperatorId), "Operator is required.");
        if (request.ShiftId == Guid.Empty)
            throw new ValidationException(nameof(request.ShiftId), "Shift is required.");
        if (!request.Date.HasValue)
            throw new ValidationException(nameof(request.Date), "Date is required.");
        if (request.Notes is { Length: > 1000 })
            throw new ValidationException(nameof(request.Notes), "Notes must be at most 1000 characters.");

        var date = request.Date.Value;

        var operatorEntity = await operatorsRepository.GetByIdAsync(request.OperatorId, cancellationToken)
            ?? throw new NotFoundException("Operator", request.OperatorId);

        var shift = await shiftsRepository.GetByIdAsync(request.ShiftId, cancellationToken)
            ?? throw new NotFoundException("Shift", request.ShiftId);

        if (await repository.ExistsAsync(request.OperatorId, request.ShiftId, date, cancellationToken))
            throw new ConflictException(
                $"Assignment for operator '{request.OperatorId}', shift '{request.ShiftId}' and date '{date:yyyy-MM-dd}' already exists.");

        var assignment = new OperatorShiftAssignment
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            OperatorId = request.OperatorId,
            ShiftId = request.ShiftId,
            Date = date,
            Notes = request.Notes,
            Operator = operatorEntity,
            Shift = shift
        };

        await repository.AddAsync(assignment, cancellationToken);
        return BrowseOperatorShiftAssignmentsRequestHandler.Map(assignment);
    }
}
