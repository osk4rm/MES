using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.RecipeVersions.Get;

internal sealed class GetRecipeVersionRequestHandler(IRecipeVersionsRepository repository)
    : IRequestHandler<GetRecipeVersionRequest, RecipeVersionDetailResponse>
{
    public async Task<RecipeVersionDetailResponse> Handle(GetRecipeVersionRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetFullAsync(request.VersionId, cancellationToken)
            ?? throw new NotFoundException("RecipeVersion", request.VersionId);
        return ProductionMappers.MapDetail(entity);
    }
}
