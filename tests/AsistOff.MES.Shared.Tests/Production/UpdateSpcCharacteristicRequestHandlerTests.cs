using AsistOff.MES.Production.Application.Features.SpcCharacteristics.Update;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class UpdateSpcCharacteristicRequestHandlerTests
{
    private readonly Mock<ISpcCharacteristicsRepository> _repository = new();

    private UpdateSpcCharacteristicRequestHandler CreateSut() => new(_repository.Object);

    private static UpdateSpcCharacteristicRequest ValidRequest(Guid id) =>
        new(id, "Shaft diameter", "Updated", null, null, SpcChartType.XbarS,
            10m, 9.5m, 10.5m, 9.8m, 10.2m, 4, "mm", false);

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFoundException()
    {
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SpcCharacteristic?)null);

        var act = () => CreateSut().Handle(ValidRequest(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_InvertedSpecLimits_ThrowsValidationException()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpcCharacteristic { Id = id, Code = "SPC-DIA", Name = "Old" });

        var request = ValidRequest(id) with { LowerSpecLimit = 10.5m, UpperSpecLimit = 9.5m };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_InvertedControlLimits_ThrowsValidationException()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpcCharacteristic { Id = id, Code = "SPC-DIA", Name = "Old" });

        var request = ValidRequest(id) with { LowerControlLimit = 10.2m, UpperControlLimit = 9.8m };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_ValidRequest_UpdatesFieldsButKeepsCode()
    {
        var id = Guid.NewGuid();
        var entity = new SpcCharacteristic { Id = id, Code = "SPC-DIA", Name = "Old", IsActive = true };
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        await CreateSut().Handle(ValidRequest(id), CancellationToken.None);

        entity.Code.Should().Be("SPC-DIA");
        entity.Name.Should().Be("Shaft diameter");
        entity.ChartType.Should().Be(SpcChartType.XbarS);
        entity.LowerSpecLimit.Should().Be(9.5m);
        entity.UpperSpecLimit.Should().Be(10.5m);
        entity.SampleSize.Should().Be(4);
        entity.IsActive.Should().BeFalse();
        _repository.Verify(r => r.UpdateAsync(entity, It.IsAny<CancellationToken>()), Times.Once);
    }
}
