using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.OperationTemplates.Delete;

public record DeleteOperationTemplateRequest(Guid Id) : ITenantRequest;
