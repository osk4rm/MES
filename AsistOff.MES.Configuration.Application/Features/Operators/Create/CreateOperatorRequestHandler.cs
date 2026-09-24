using AsistOff.MES.Configuration.Application.Features.Operators.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Configuration.Application.Features.Operators.Create;

internal sealed class CreateOperatorRequestHandler(
    IOperatorsRepository operatorsRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext,
    ILogger<CreateOperatorRequestHandler> logger)
    : IRequestHandler<CreateOperatorRequest, OperatorResponse>
{
    public async Task<OperatorResponse> Handle(CreateOperatorRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
            throw new ValidationException(nameof(request.Identifier), "Identifier is required.");
        if (string.IsNullOrWhiteSpace(request.FirstName))
            throw new ValidationException(nameof(request.FirstName), "First name is required.");
        if (string.IsNullOrWhiteSpace(request.LastName))
            throw new ValidationException(nameof(request.LastName), "Last name is required.");

        var operatorEntity = new Operator
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            Identifier = request.Identifier,
            FirstName = request.FirstName,
            LastName = request.LastName,
            RatePerHour = request.RatePerHour,
            DepartmentId = request.DepartmentId,
            UserId = request.UserId
        };

        try
        {
            var createdOperator = await operatorsRepository.AddAsync(operatorEntity, cancellationToken);

            logger.LogInformation("Created operator with ID {OperatorId} for tenant {TenantId}",
                createdOperator.Id, tenantContext.TenantId);

            return new OperatorResponse
            {
                Id = createdOperator.Id,
                Identifier = createdOperator.Identifier,
                FirstName = createdOperator.FirstName,
                LastName = createdOperator.LastName,
                RatePerHour = createdOperator.RatePerHour,
                Department = createdOperator.Department?.Name
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to create operator with request {Request}", request);
            throw;
        }
    }
}