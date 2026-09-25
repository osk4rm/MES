using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.ScrapEvents.Update;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record UpdateScrapEventRequest(
    Guid Id,
    Guid ReasonCodeId,
    decimal Quantity,
    string? Notes) : ITenantRequest;
