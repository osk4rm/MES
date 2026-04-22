using AsistOff.MES.Configuration.Application.Features.Departments.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Departments.Get;

public record GetDepartmentRequest(Guid DepartmentId) : ITenantRequest<DepartmentResponse>;
