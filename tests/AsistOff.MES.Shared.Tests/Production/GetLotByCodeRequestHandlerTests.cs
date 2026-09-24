using AsistOff.MES.Production.Application.Features.Lots.GetByCode;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class GetLotByCodeRequestHandlerTests
{
    private readonly Mock<ILotsRepository> _repository = new();

    private GetLotByCodeRequestHandler CreateSut() => new(_repository.Object);

    [Fact]
    public async Task Handle_KnownCode_ReturnsLot()
    {
        var lot = new Lot
        {
            Id = Guid.NewGuid(),
            Code = "LOT-1",
            ProductId = Guid.NewGuid(),
            MeasureUnitId = Guid.NewGuid(),
            Quantity = 7m,
            Status = LotStatus.Available
        };
        _repository
            .Setup(r => r.GetByCodeAsync("LOT-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(lot);

        var result = await CreateSut().Handle(new GetLotByCodeRequest("LOT-1"), CancellationToken.None);

        result.Code.Should().Be("LOT-1");
        result.Status.Should().Be(LotStatus.Available);
    }

    [Fact]
    public async Task Handle_UnknownCode_ThrowsNotFoundException()
    {
        _repository
            .Setup(r => r.GetByCodeAsync("MISSING", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lot?)null);

        var act = () => CreateSut().Handle(new GetLotByCodeRequest("MISSING"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
