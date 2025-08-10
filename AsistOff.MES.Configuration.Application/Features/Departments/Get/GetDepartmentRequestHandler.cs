using AsistOff.MES.Configuration.Application.Features.Departments.Responses;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Departments.Get;

internal sealed class GetDepartmentRequestHandler(
    IDepartmentsRepository departmentsRepository)
    : IRequestHandler<GetDepartmentRequest, DepartmentResponse>
{
    public async Task<DepartmentResponse> Handle(GetDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = await departmentsRepository.GetByIdAsync(request.DepartmentId, cancellationToken);

        if (department is null)
        {
            throw new NotFoundException("Department", request.DepartmentId);
        }

        return new DepartmentResponse
        {
            Id = department.Id,
            Code = department.Code,
            Name = department.Name
        };
    }
}
