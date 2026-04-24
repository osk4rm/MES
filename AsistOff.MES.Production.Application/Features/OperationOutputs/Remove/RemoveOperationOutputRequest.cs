using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.OperationOutputs.Remove;

public record RemoveOperationOutputRequest(Guid OutputId) : ITenantRequest;
