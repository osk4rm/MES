using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.RecipeVersions.Release;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record ReleaseRecipeVersionRequest(Guid VersionId) : ITenantRequest;
