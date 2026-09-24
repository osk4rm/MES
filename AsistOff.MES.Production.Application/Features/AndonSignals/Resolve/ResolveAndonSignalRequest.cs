using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.AndonSignals.Resolve;

public record ResolveAndonSignalRequest(Guid Id, DateTime? ResolvedAt) : ITenantRequest<AndonSignalResponse>;
