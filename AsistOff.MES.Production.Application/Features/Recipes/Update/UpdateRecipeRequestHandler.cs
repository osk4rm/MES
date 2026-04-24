using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Recipes.Update;

internal sealed class UpdateRecipeRequestHandler(IRecipesRepository repository)
    : IRequestHandler<UpdateRecipeRequest>
{
    public async Task Handle(UpdateRecipeRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Recipe", request.Id);

        if (!string.Equals(entity.Code, request.Code, StringComparison.Ordinal) &&
            await repository.CodeExistsAsync(request.Code, request.Id, cancellationToken))
        {
            throw new ConflictException($"Recipe with code '{request.Code}' already exists.");
        }

        entity.Code = request.Code;
        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.IsActive = request.IsActive;
        entity.PrimaryProductId = request.PrimaryProductId;
        entity.SyncId = request.SyncId;

        await repository.UpdateAsync(entity, cancellationToken);
    }
}
