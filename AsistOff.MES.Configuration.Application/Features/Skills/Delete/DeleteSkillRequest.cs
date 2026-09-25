using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using MediatR;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.Skills.Delete;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record DeleteSkillRequest(Guid Id) : ITenantRequest;
