using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Kanban.Loops.Create;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class CreateKanbanLoopRequestHandlerTests
{
    private readonly Mock<IKanbanLoopsRepository> _repository = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();

    public CreateKanbanLoopRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);
        _tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
        _repository
            .Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private CreateKanbanLoopRequestHandler CreateSut() =>
        new(_repository.Object, _guids.Object, _clock.Object, _tenant.Object);

    private static CreateKanbanLoopRequest ValidRequest(string code = "KB-1") =>
        new(code, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, 2, null);

    [Fact]
    public async Task Handle_ValidRequest_CreatesActiveLoopWithCallerTenant()
    {
        var tenantId = Guid.NewGuid();
        _tenant.SetupGet(t => t.TenantId).Returns(tenantId);

        KanbanLoop? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<KanbanLoop>(), It.IsAny<CancellationToken>()))
            .Callback<KanbanLoop, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((KanbanLoop entity, CancellationToken _) => entity);

        var result = await CreateSut().Handle(ValidRequest("KB-9"), CancellationToken.None);

        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(tenantId);
        persisted.Code.Should().Be("KB-9");
        persisted.IsActive.Should().BeTrue();
        persisted.CardQuantity.Should().Be(10m);
        persisted.CardsInCirculation.Should().Be(2);
        result.Code.Should().Be("KB-9");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DuplicateCode_ThrowsConflictException()
    {
        _repository
            .Setup(r => r.CodeExistsAsync("KB-1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => CreateSut().Handle(ValidRequest("KB-1"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ZeroCardQuantity_ThrowsValidationException()
    {
        var request = ValidRequest() with { CardQuantity = 0m };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_NegativeCardQuantity_ThrowsValidationException()
    {
        var request = ValidRequest() with { CardQuantity = -5m };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Handle_CardsInCirculationOutOfBounds_ThrowsValidationException(int circulation)
    {
        var request = ValidRequest() with { CardsInCirculation = circulation };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_MissingCode_ThrowsValidationException()
    {
        var act = () => CreateSut().Handle(ValidRequest("  "), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_NotesExceedingMaxLength_ThrowsValidationException()
    {
        var request = ValidRequest() with { Notes = new string('N', 1001) };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
