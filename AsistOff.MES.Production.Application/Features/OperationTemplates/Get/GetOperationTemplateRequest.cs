using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.OperationTemplates.Get;

public record GetOperationTemplateRequest(Guid Id) : ITenantRequest<OperationTemplateResponse>;
