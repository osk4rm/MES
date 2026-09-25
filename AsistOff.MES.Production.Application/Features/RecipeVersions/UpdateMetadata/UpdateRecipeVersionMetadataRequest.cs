using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.RecipeVersions.UpdateMetadata;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record UpdateRecipeVersionMetadataRequest(
    Guid VersionId,
    string? ChangeNotes,
    DateTime? ValidFrom,
    DateTime? ValidTo) : ITenantRequest;
