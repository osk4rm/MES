using AsistOff.MES.Multitenancy.Requests;
using ErrorOr;
using MediatR;

namespace AsistOff.MES.Users.Application.Features.Users.Create;

public record CreateUserRequest(
    string Email,
    string Password,
    string ConfirmPassword,
    string? FirstName,
    string? LastName
    ) : ITenantRequest<ErrorOr<Unit>>;