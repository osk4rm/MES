using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.RecipeVersions.Create;

internal sealed class CreateRecipeVersionRequestHandler(
    IRecipesRepository recipesRepository,
    IRecipeVersionsRepository versionsRepository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateRecipeVersionRequest, RecipeVersionDetailResponse>
{
    public async Task<RecipeVersionDetailResponse> Handle(CreateRecipeVersionRequest request, CancellationToken cancellationToken)
    {
        var recipe = await recipesRepository.GetAsync(request.RecipeId, cancellationToken)
            ?? throw new NotFoundException("Recipe", request.RecipeId);

        var nextNumber = await versionsRepository.GetNextVersionNumberAsync(recipe.Id, cancellationToken);

        var version = new RecipeVersion
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            RecipeId = recipe.Id,
            VersionNumber = nextNumber,
            Status = RecipeVersionStatus.Draft,
            ChangeNotes = request.ChangeNotes,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            CreatedAt = dateTimeProvider.UtcNow
        };

        await versionsRepository.AddAsync(version, cancellationToken);

        var full = await versionsRepository.GetFullAsync(version.Id, cancellationToken)!;
        return ProductionMappers.MapDetail(full!);
    }
}
