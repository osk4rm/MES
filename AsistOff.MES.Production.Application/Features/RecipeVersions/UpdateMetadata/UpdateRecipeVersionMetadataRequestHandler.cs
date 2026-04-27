using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.RecipeVersions.UpdateMetadata;

internal sealed class UpdateRecipeVersionMetadataRequestHandler(
    IRecipeVersionsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateRecipeVersionMetadataRequest>
{
    public async Task Handle(UpdateRecipeVersionMetadataRequest request, CancellationToken cancellationToken)
    {
        var version = await repository.GetAsync(request.VersionId, cancellationToken)
            ?? throw new NotFoundException("RecipeVersion", request.VersionId);

        if (version.Status == RecipeVersionStatus.Obsolete)
            throw new ConflictException("Obsolete versions cannot be modified.");

        version.ChangeNotes = request.ChangeNotes;
        version.ValidFrom = request.ValidFrom;
        version.ValidTo = request.ValidTo;
        version.UpdatedAt = dateTimeProvider.UtcNow;

        await repository.UpdateAsync(version, cancellationToken);
    }
}
