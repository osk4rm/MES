using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.Common;

public record RecipeVersionSummaryResponse(
    Guid Id,
    int VersionNumber,
    RecipeVersionStatus Status,
    DateTime? ReleasedAt,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    string? ChangeNotes,
    DateTime CreatedAt);

public record RecipeResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    Guid? PrimaryProductId,
    Guid? CurrentVersionId,
    string? SyncId,
    IReadOnlyCollection<RecipeVersionSummaryResponse> Versions);
