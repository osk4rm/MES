using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Configuration.Application.Features.Departments.Delete;

internal sealed class DeleteDepartmentRequestHandler(
    IDepartmentsRepository departmentsRepository,
    ILogger<DeleteDepartmentRequestHandler> logger)
    : IRequestHandler<DeleteDepartmentRequest>
{
    public async Task Handle(DeleteDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = await departmentsRepository.GetByIdAsync(request.Id, cancellationToken);

        if (department is null)
        {
            throw new NotFoundException("Department", request.Id);
        }

        try
        {
            await departmentsRepository.DeleteAsync(request.Id, cancellationToken);
            logger.LogInformation("Deleted department with ID {DepartmentId}", request.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to delete department with ID {DepartmentId}", request.Id);
            throw;
        }
    }
}
