using AsistOff.MES.Multitenancy.Entity;
using MediatR;

namespace AsistOff.MES.Multitenancy.Requests.Commands.Create;

public class CreateTenantCommand : IRequest<Tenant>
{
    public required string Name { get; set; }
    public string? DisplayName { get; set; }
    public string? ContactEmail { get; set; }
    public string? Settings { get; set; }
}

