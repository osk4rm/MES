using AsistOff.MES.Production.Application.Features.Lots.Update;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class UpdateLotRequestHandlerTests
{
    private readonly Mock<ILotsRepository> _repository = new();
    private readonly Mock<IDateTimeProvider> _clock = new();

    public UpdateLotRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);
        _repository
            .Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private UpdateLotRequestHandler CreateSut() =>
        new(_repository.Object, _clock.Object);

    private void SetupExistingLot(Guid id) =>
        _repository
            .Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Lot
            {
                Id = id,
                Code = "LOT-1",
                ProductId = Guid.NewGuid(),
                MeasureUnitId = Guid.NewGuid(),
                Quantity = 5m
            });

    private static UpdateLotRequest ValidRequest(Guid id, string code = "LOT-1") =>
        new(id, code, Guid.NewGuid(), Guid.NewGuid(), 10m, null, null, null, null);

    [Fact]
    public async Task Handle_CodeExceedingMaxLength_ThrowsValidationException()
    {
        var id = Guid.NewGuid();
        SetupExistingLot(id);

        var act = () => CreateSut().Handle(ValidRequest(id, new string('L', 51)), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_SupplierLotNumberExceedingMaxLength_ThrowsValidationException()
    {
        var id = Guid.NewGuid();
        SetupExistingLot(id);
        var request = ValidRequest(id) with { SupplierLotNumber = new string('S', 101) };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_NotesExceedingMaxLength_ThrowsValidationException()
    {
        var id = Guid.NewGuid();
        SetupExistingLot(id);
        var request = ValidRequest(id) with { Notes = new string('N', 1001) };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_UnknownLot_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lot?)null);

        var act = () => CreateSut().Handle(ValidRequest(id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
