using AsistOff.MES.Multitenancy.Contracts;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using MediatR;

namespace AsistOff.MES.Multitenancy.Requests.Commands.Create;

public record CreateTenantCommand(
    string Name,
    string DisplayName,
    string ContactEmail,
    string Settings,
    string Password,
    string ConfirmPassword
    ) : IRequest<AnonymousTenantResponse>, IAllowAnonymousRequest;
