using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Multitenancy.Requests;
using AsistOff.MES.Shared.Abstractions.Auth;
using MediatR;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Users.Application.Features.Users.Create;

[RequirePermission(RbacDefaults.UsersWrite)]
public record CreateUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string ConfirmPassword
    ) : ITenantRequest<Unit>;
