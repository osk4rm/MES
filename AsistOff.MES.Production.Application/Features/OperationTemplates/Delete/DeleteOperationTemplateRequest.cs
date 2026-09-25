using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.OperationTemplates.Delete;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record DeleteOperationTemplateRequest(Guid Id) : ITenantRequest;
