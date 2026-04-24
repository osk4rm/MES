using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Recipes.Create;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class CreateRecipeRequestHandlerTests
{
    private readonly Mock<IRecipesRepository> _recipes = new();
    private readonly Mock<IRecipeVersionsRepository> _versions = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();

    public CreateRecipeRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);
        _tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
    }

    private CreateRecipeRequestHandler CreateSut() =>
        new(_recipes.Object, _versions.Object, _guids.Object, _clock.Object, _tenant.Object);

    [Fact]
    public async Task Throws_ValidationException_when_code_is_empty()
    {
        var request = new CreateRecipeRequest("", "Name", null, true, null, null);

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Throws_ConflictException_when_code_already_exists()
    {
        _recipes.Setup(r => r.CodeExistsAsync("CODE", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreateRecipeRequest("CODE", "Name", null, true, null, null);
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Creates_recipe_together_with_initial_draft_version()
    {
        _recipes.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _recipes.Setup(r => r.GetWithVersionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new Recipe { Id = id, Code = "CODE", Name = "Name" });

        var request = new CreateRecipeRequest("CODE", "Name", null, true, null, null);
        await CreateSut().Handle(request, CancellationToken.None);

        _recipes.Verify(r => r.AddAsync(It.IsAny<Recipe>(), It.IsAny<CancellationToken>()), Times.Once);
        _versions.Verify(v => v.AddAsync(
            It.Is<RecipeVersion>(rv => rv.VersionNumber == 1 &&
                                       rv.Status == AsistOff.MES.Production.Domain.Enums.RecipeVersionStatus.Draft),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
