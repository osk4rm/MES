using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Multitenancy.Requests;
using MediatR;

namespace AsistOff.MES.Users.Application.Features.Users.Create;

public record CreateUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string ConfirmPassword
    ) : ITenantRequest<Unit>;
