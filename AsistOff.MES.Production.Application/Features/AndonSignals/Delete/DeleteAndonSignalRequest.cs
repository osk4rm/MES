using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.AndonSignals.Delete;

public record DeleteAndonSignalRequest(Guid Id) : ITenantRequest;
