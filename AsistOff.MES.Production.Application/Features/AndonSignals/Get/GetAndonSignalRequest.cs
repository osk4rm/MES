using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.AndonSignals.Get;

public record GetAndonSignalRequest(Guid Id) : ITenantRequest<AndonSignalResponse>;
