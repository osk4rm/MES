using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;

namespace AsistOff.MES.Production.Application.Features.Common;

internal static class VersionGuard
{
    /// <summary>
    /// Ensures the given version exists and is in <see cref="RecipeVersionStatus.Draft"/>.
    /// Released and Obsolete versions are immutable.
    /// </summary>
    public static async Task<RecipeVersion> EnsureDraftAsync(
        IRecipeVersionsRepository repository, Guid versionId, CancellationToken cancellationToken)
    {
        var version = await repository.GetAsync(versionId, cancellationToken)
            ?? throw new NotFoundException("RecipeVersion", versionId);

        if (version.Status != RecipeVersionStatus.Draft)
            throw new ConflictException("Only Draft versions can be modified.");

        return version;
    }
}
