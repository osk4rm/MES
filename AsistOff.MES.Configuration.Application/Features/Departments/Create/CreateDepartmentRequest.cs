using AsistOff.MES.Configuration.Application.Features.Departments.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Departments.Create;

public record CreateDepartmentRequest(
    string Code,
    string Name
) : ITenantRequest<DepartmentResponse>;
