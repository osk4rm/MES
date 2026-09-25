using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.RecipeVersions.Delete;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record DeleteRecipeVersionRequest(Guid VersionId) : ITenantRequest;
