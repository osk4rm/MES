using AsistOff.MES.Production.Application.Features.SpcMeasurements.Chart;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class GetSpcMeasurementChartRequestHandlerTests
{
    private readonly Mock<ISpcCharacteristicsRepository> _characteristics = new();
    private readonly Mock<ISpcMeasurementsRepository> _measurements = new();
    private readonly Guid _characteristicId = Guid.NewGuid();

    private GetSpcMeasurementChartRequestHandler CreateSut() =>
        new(_characteristics.Object, _measurements.Object);

    private static SpcCharacteristic Characteristic(
        Guid id,
        decimal? lsl = 9.5m,
        decimal? usl = 10.5m,
        decimal? lcl = 9.8m,
        decimal? ucl = 10.2m) => new()
        {
            Id = id,
            Code = "SPC-DIA",
            Name = "Shaft diameter",
            NominalValue = 10m,
            LowerSpecLimit = lsl,
            UpperSpecLimit = usl,
            LowerControlLimit = lcl,
            UpperControlLimit = ucl,
            IsActive = true
        };

    private void Setup(SpcCharacteristic characteristic, params SpcMeasurement[] rows)
    {
        _characteristics.Setup(r => r.GetByIdAsync(characteristic.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(characteristic);
        _measurements.Setup(r => r.ListForCharacteristicAsync(
                characteristic.Id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);
    }

    private static SpcMeasurement Row(Guid characteristicId, decimal value, string time) => new()
    {
        Id = Guid.NewGuid(),
        CharacteristicId = characteristicId,
        Value = value,
        MeasuredAt = DateTime.Parse(time).ToUniversalTime()
    };

    [Fact]
    public async Task Handle_UnknownCharacteristic_ThrowsNotFoundException()
    {
        _characteristics.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SpcCharacteristic?)null);

        var act = () => CreateSut().Handle(
            new GetSpcMeasurementChartRequest { CharacteristicId = Guid.NewGuid() }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_PointBeyondControlLimits_MarksOutOfControlAndOutOfSpec()
    {
        var characteristic = Characteristic(_characteristicId);
        Setup(characteristic,
            Row(_characteristicId, 10.3m, "2026-09-25T10:00:00Z"),
            Row(_characteristicId, 10.0m, "2026-09-25T11:00:00Z"));

        var result = await CreateSut().Handle(
            new GetSpcMeasurementChartRequest { CharacteristicId = _characteristicId }, CancellationToken.None);

        result.Points.Should().HaveCount(2);
        result.Points.First().IsOutOfControl.Should().BeTrue();
        result.Points.First().IsOutOfSpec.Should().BeFalse();
        result.OutOfControlCount.Should().Be(1);
        result.OutOfSpecCount.Should().Be(0);
        result.TotalCount.Should().Be(2);
        result.LowerControlLimit.Should().Be(9.8m);
        result.UpperControlLimit.Should().Be(10.2m);
    }

    [Fact]
    public async Task Handle_PointBeyondSpecLimits_MarksOutOfSpec()
    {
        var characteristic = Characteristic(_characteristicId);
        Setup(characteristic, Row(_characteristicId, 10.9m, "2026-09-25T10:00:00Z"));

        var result = await CreateSut().Handle(
            new GetSpcMeasurementChartRequest { CharacteristicId = _characteristicId }, CancellationToken.None);

        var point = result.Points.Should().ContainSingle().Subject;
        point.IsOutOfControl.Should().BeTrue();
        point.IsOutOfSpec.Should().BeTrue();
        result.OutOfControlCount.Should().Be(1);
        result.OutOfSpecCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_PointWithinLimits_MarksNeitherFlag()
    {
        var characteristic = Characteristic(_characteristicId);
        Setup(characteristic, Row(_characteristicId, 10.0m, "2026-09-25T10:00:00Z"));

        var result = await CreateSut().Handle(
            new GetSpcMeasurementChartRequest { CharacteristicId = _characteristicId }, CancellationToken.None);

        var point = result.Points.Should().ContainSingle().Subject;
        point.IsOutOfControl.Should().BeFalse();
        point.IsOutOfSpec.Should().BeFalse();
        result.OutOfControlCount.Should().Be(0);
        result.OutOfSpecCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_AbsentControlLimits_NeverMarksOutOfControl()
    {
        var characteristic = Characteristic(_characteristicId, lcl: null, ucl: null);
        Setup(characteristic, Row(_characteristicId, 99.99m, "2026-09-25T10:00:00Z"));

        var result = await CreateSut().Handle(
            new GetSpcMeasurementChartRequest { CharacteristicId = _characteristicId }, CancellationToken.None);

        var point = result.Points.Should().ContainSingle().Subject;
        point.IsOutOfControl.Should().BeFalse();
        point.IsOutOfSpec.Should().BeTrue();
        result.OutOfControlCount.Should().Be(0);
    }
}
