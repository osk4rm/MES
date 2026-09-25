using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using MediatR;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.SpcCharacteristics.Delete;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record DeleteSpcCharacteristicRequest(Guid Id) : ITenantRequest;
