using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Operations.Delete;

public record DeleteOperationRequest(Guid OperationId) : ITenantRequest;
