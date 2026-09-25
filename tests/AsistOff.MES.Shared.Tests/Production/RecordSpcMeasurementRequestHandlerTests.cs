using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.SpcMeasurements.Record;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class RecordSpcMeasurementRequestHandlerTests
{
    private readonly Mock<ISpcMeasurementsRepository> _repository = new();
    private readonly Mock<ISpcCharacteristicsRepository> _characteristics = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
    private readonly Guid _characteristicId = Guid.NewGuid();

    public RecordSpcMeasurementRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _characteristics.Setup(r => r.GetByIdAsync(_characteristicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveCharacteristic());
    }

    private RecordSpcMeasurementRequestHandler CreateSut() =>
        new(_repository.Object, _characteristics.Object, _guids.Object, _clock.Object, _tenant.Object);

    private SpcCharacteristic ActiveCharacteristic() => new()
    {
        Id = _characteristicId,
        TenantId = _tenantId,
        Code = "SPC-DIA",
        Name = "Shaft diameter",
        IsActive = true
    };

    private static RecordSpcMeasurementRequest ValidRequest(Guid characteristicId) => new(
        characteristicId,
        10.01m,
        new DateTime(2026, 9, 25, 11, 0, 0, DateTimeKind.Utc),
        "ok");

    [Fact]
    public async Task Handle_ValidRequest_PersistsValueAndTenant()
    {
        SpcMeasurement? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<SpcMeasurement>(), It.IsAny<CancellationToken>()))
            .Callback<SpcMeasurement, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((SpcMeasurement e, CancellationToken _) => e);

        var result = await CreateSut().Handle(ValidRequest(_characteristicId), CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.CharacteristicId.Should().Be(_characteristicId);
        saved.Value.Should().Be(10.01m);
        saved.TenantId.Should().Be(_tenantId);
        result.CharacteristicId.Should().Be(_characteristicId);
        result.Value.Should().Be(10.01m);
    }

    [Fact]
    public async Task Handle_UnknownCharacteristic_ThrowsNotFoundException()
    {
        _characteristics.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SpcCharacteristic?)null);

        var act = () => CreateSut().Handle(ValidRequest(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_InactiveCharacteristic_ThrowsValidationException()
    {
        var inactive = ActiveCharacteristic();
        inactive.IsActive = false;
        _characteristics.Setup(r => r.GetByIdAsync(_characteristicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactive);

        var act = () => CreateSut().Handle(ValidRequest(_characteristicId), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_MeasuredAtBeyondTolerance_ThrowsValidationException()
    {
        var request = ValidRequest(_characteristicId) with { MeasuredAt = _now.AddMinutes(6) };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_MeasuredAtWithinTolerance_DoesNotThrow()
    {
        SpcMeasurement? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<SpcMeasurement>(), It.IsAny<CancellationToken>()))
            .Callback<SpcMeasurement, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((SpcMeasurement e, CancellationToken _) => e);
        var request = ValidRequest(_characteristicId) with { MeasuredAt = _now.AddMinutes(4) };

        var result = await CreateSut().Handle(request, CancellationToken.None);

        saved.Should().NotBeNull();
        result.MeasuredAt.Should().BeCloseTo(_now.AddMinutes(4), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Handle_DefaultMeasuredAt_ThrowsValidationException()
    {
        var request = ValidRequest(_characteristicId) with { MeasuredAt = default };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
