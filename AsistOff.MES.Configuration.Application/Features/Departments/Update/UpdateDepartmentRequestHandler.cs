using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Configuration.Application.Features.Departments.Update;

internal sealed class UpdateDepartmentRequestHandler(
    IDepartmentsRepository departmentsRepository,
    ILogger<UpdateDepartmentRequestHandler> logger)
    : IRequestHandler<UpdateDepartmentRequest>
{
    public async Task Handle(UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = await departmentsRepository.GetByIdAsync(request.Id, cancellationToken);

        if (department is null)
        {
            throw new NotFoundException("Department", request.Id);
        }
        
        department.Code = request.Code;
        department.Name = request.Name;

        try
        {
            await departmentsRepository.UpdateAsync(department, cancellationToken);
            
            logger.LogInformation("Updated department with ID {DepartmentId}", department.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to update department with ID {DepartmentId}", request.Id);
            throw;
        }
    }
}
