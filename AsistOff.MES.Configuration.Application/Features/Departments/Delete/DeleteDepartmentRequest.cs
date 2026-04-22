using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Departments.Delete;

public record DeleteDepartmentRequest(Guid Id) : IRequest;
