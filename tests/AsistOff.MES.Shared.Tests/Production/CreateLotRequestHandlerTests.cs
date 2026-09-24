using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Lots.Create;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class CreateLotRequestHandlerTests
{
    private readonly Mock<ILotsRepository> _repository = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();

    public CreateLotRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);
        _tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
        _repository
            .Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private CreateLotRequestHandler CreateSut() =>
        new(_repository.Object, _guids.Object, _clock.Object, _tenant.Object);

    private static CreateLotRequest ValidRequest(string code = "LOT-1") =>
        new(code, Guid.NewGuid(), Guid.NewGuid(), 10m, null, null, null, null);

    [Fact]
    public async Task Handle_MissingCode_ThrowsValidationException()
    {
        var act = () => CreateSut().Handle(ValidRequest("  "), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_NegativeQuantity_ThrowsValidationException()
    {
        var request = ValidRequest() with { Quantity = -1m };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_CodeExceedingMaxLength_ThrowsValidationException()
    {
        var request = ValidRequest(new string('L', 51));

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_SupplierLotNumberExceedingMaxLength_ThrowsValidationException()
    {
        var request = ValidRequest() with { SupplierLotNumber = new string('S', 101) };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_NotesExceedingMaxLength_ThrowsValidationException()
    {
        var request = ValidRequest() with { Notes = new string('N', 1001) };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_DuplicateCode_ThrowsConflictException()
    {
        _repository
            .Setup(r => r.CodeExistsAsync("LOT-1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => CreateSut().Handle(ValidRequest("LOT-1"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ValidRequest_SetsTenantIdAndStatusAvailable()
    {
        var tenantId = Guid.NewGuid();
        _tenant.SetupGet(t => t.TenantId).Returns(tenantId);

        Lot? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Lot>(), It.IsAny<CancellationToken>()))
            .Callback<Lot, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((Lot entity, CancellationToken _) => entity);

        var result = await CreateSut().Handle(ValidRequest("LOT-9"), CancellationToken.None);

        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(tenantId);
        persisted.Status.Should().Be(LotStatus.Available);
        persisted.Code.Should().Be("LOT-9");
        result.Status.Should().Be(LotStatus.Available);
        result.Code.Should().Be("LOT-9");
    }
}
