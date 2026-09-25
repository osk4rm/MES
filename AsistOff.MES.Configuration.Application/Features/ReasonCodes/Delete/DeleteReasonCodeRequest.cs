using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using MediatR;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.ReasonCodes.Delete;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record DeleteReasonCodeRequest(Guid Id) : ITenantRequest;
