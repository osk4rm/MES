using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.AndonSignals.Acknowledge;

public record AcknowledgeAndonSignalRequest(Guid Id) : ITenantRequest<AndonSignalResponse>;
