using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.RecipeVersions.Delete;

internal sealed class DeleteRecipeVersionRequestHandler(IRecipeVersionsRepository repository)
    : IRequestHandler<DeleteRecipeVersionRequest>
{
    public async Task Handle(DeleteRecipeVersionRequest request, CancellationToken cancellationToken)
    {
        var version = await repository.GetAsync(request.VersionId, cancellationToken)
            ?? throw new NotFoundException("RecipeVersion", request.VersionId);

        if (version.Status != RecipeVersionStatus.Draft)
            throw new ConflictException("Only Draft versions can be deleted. Use clone+release to supersede a released version.");

        await repository.DeleteAsync(version.Id, cancellationToken);
    }
}
