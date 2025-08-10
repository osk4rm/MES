using AsistOff.MES.Configuration.Application.Features.Departments.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Configuration.Application.Features.Departments.Create;

internal sealed class CreateDepartmentRequestHandler(
    IDepartmentsRepository departmentsRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext,
    ILogger<CreateDepartmentRequestHandler> logger)
    : IRequestHandler<CreateDepartmentRequest, DepartmentResponse>
{
    public async Task<DepartmentResponse> Handle(CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = new Department
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            Code = request.Code,
            Name = request.Name
        };

        try
        {
            var createdDepartment = await departmentsRepository.AddAsync(department, cancellationToken);
            
            logger.LogInformation("Created department with ID {DepartmentId} for tenant {TenantId}", 
                createdDepartment.Id, tenantContext.TenantId);

            return new DepartmentResponse
            {
                Id = createdDepartment.Id,
                Code = createdDepartment.Code,
                Name = createdDepartment.Name
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to create department with request {Request}", request);
            throw;
        }
    }
}
