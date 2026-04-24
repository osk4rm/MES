using AsistOff.MES.Production.Application.Features.RecipeVersions.Release;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class ReleaseRecipeVersionRequestHandlerTests
{
    private readonly Mock<IRecipeVersionsRepository> _versions = new();
    private readonly Mock<IRecipesRepository> _recipes = new();
    private readonly Mock<IDateTimeProvider> _clock = new();

    public ReleaseRecipeVersionRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);
    }

    private ReleaseRecipeVersionRequestHandler CreateSut() =>
        new(_versions.Object, _recipes.Object, _clock.Object);

    [Fact]
    public async Task Throws_NotFoundException_when_version_missing()
    {
        var id = Guid.NewGuid();
        _versions.Setup(v => v.GetFullAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecipeVersion?)null);

        var act = () => CreateSut().Handle(new ReleaseRecipeVersionRequest(id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Throws_ConflictException_when_version_is_not_draft()
    {
        var version = MakeVersion(RecipeVersionStatus.Released, withOperations: true);
        _versions.Setup(v => v.GetFullAsync(version.Id, It.IsAny<CancellationToken>())).ReturnsAsync(version);

        var act = () => CreateSut().Handle(new ReleaseRecipeVersionRequest(version.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Throws_ValidationException_when_version_has_no_operations()
    {
        var version = MakeVersion(RecipeVersionStatus.Draft, withOperations: false);
        _versions.Setup(v => v.GetFullAsync(version.Id, It.IsAny<CancellationToken>())).ReturnsAsync(version);

        var act = () => CreateSut().Handle(new ReleaseRecipeVersionRequest(version.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Demotes_previous_released_version_and_points_recipe_at_new_version()
    {
        var recipeId = Guid.NewGuid();
        var draft = MakeVersion(RecipeVersionStatus.Draft, withOperations: true, recipeId: recipeId);
        var previousReleased = MakeVersion(RecipeVersionStatus.Released, withOperations: true, recipeId: recipeId);
        var recipe = new Recipe { Id = recipeId, Code = "R", Name = "R" };

        _versions.Setup(v => v.GetFullAsync(draft.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draft);
        _versions.Setup(v => v.ListForRecipeAsync(recipeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { draft, previousReleased });
        _recipes.Setup(r => r.GetAsync(recipeId, It.IsAny<CancellationToken>())).ReturnsAsync(recipe);

        await CreateSut().Handle(new ReleaseRecipeVersionRequest(draft.Id), CancellationToken.None);

        draft.Status.Should().Be(RecipeVersionStatus.Released);
        draft.ReleasedAt.Should().NotBeNull();
        previousReleased.Status.Should().Be(RecipeVersionStatus.Obsolete);
        recipe.CurrentVersionId.Should().Be(draft.Id);

        _versions.Verify(v => v.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _recipes.Verify(r => r.UpdateAsync(recipe, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static RecipeVersion MakeVersion(RecipeVersionStatus status, bool withOperations, Guid? recipeId = null)
    {
        var version = new RecipeVersion
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeId ?? Guid.NewGuid(),
            VersionNumber = 1,
            Status = status
        };
        if (withOperations)
        {
            version.Operations.Add(new OperationNode
            {
                Id = Guid.NewGuid(),
                RecipeVersionId = version.Id,
                Code = "OP",
                Name = "OP",
                SortIndex = 0
            });
        }
        return version;
    }
}
