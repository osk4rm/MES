using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Departments.Update;

public record UpdateDepartmentRequest(
    Guid Id,
    string Code,
    string Name
) : IRequest;
