using ErrorOr;
using MediatR;

namespace AsistOff.MES.Multitenancy.Requests.Commands.Create;

public record CreateTenantCommand(
    string Name,
    string? DisplayName,
    string? ContactEmail,
    string? Settings) : IRequest<ErrorOr<Guid>>;