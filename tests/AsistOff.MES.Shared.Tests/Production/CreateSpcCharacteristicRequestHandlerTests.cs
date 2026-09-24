using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.SpcCharacteristics.Create;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class CreateSpcCharacteristicRequestHandlerTests
{
    private readonly Mock<ISpcCharacteristicsRepository> _repository = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<ITenantContext> _tenant = new();

    public CreateSpcCharacteristicRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
        _repository
            .Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private CreateSpcCharacteristicRequestHandler CreateSut() =>
        new(_repository.Object, _guids.Object, _tenant.Object);

    private static CreateSpcCharacteristicRequest ValidRequest(
        string code = "SPC-DIA",
        SpcChartType chartType = SpcChartType.XbarR,
        decimal? nominal = 10m,
        decimal? lsl = 9.5m,
        decimal? usl = 10.5m,
        decimal? lcl = 9.8m,
        decimal? ucl = 10.2m,
        int sampleSize = 5) =>
        new(code, "Shaft diameter", null, null, null, chartType,
            nominal, lsl, usl, lcl, ucl, sampleSize, "mm", true);

    [Fact]
    public async Task Handle_DuplicateCode_ThrowsConflictException()
    {
        _repository
            .Setup(r => r.CodeExistsAsync("SPC-DIA", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => CreateSut().Handle(ValidRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_InvertedSpecLimits_ThrowsValidationException()
    {
        var request = ValidRequest(lsl: 10.5m, usl: 9.5m);

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_InvertedControlLimits_ThrowsValidationException()
    {
        var request = ValidRequest(lcl: 10.2m, ucl: 9.8m);

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_NominalOutsideSpecLimits_ThrowsValidationException()
    {
        var request = ValidRequest(nominal: 11m);

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_SampleSizeBelowOne_ThrowsValidationException()
    {
        var request = ValidRequest(sampleSize: 0);

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_UndefinedChartType_ThrowsValidationException()
    {
        var request = ValidRequest(chartType: (SpcChartType)99);

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsLimitsChartTypeAndTenantId()
    {
        var tenantId = Guid.NewGuid();
        _tenant.SetupGet(t => t.TenantId).Returns(tenantId);

        SpcCharacteristic? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<SpcCharacteristic>(), It.IsAny<CancellationToken>()))
            .Callback<SpcCharacteristic, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((SpcCharacteristic entity, CancellationToken _) => entity);

        var result = await CreateSut().Handle(ValidRequest(), CancellationToken.None);

        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(tenantId);
        persisted.ChartType.Should().Be(SpcChartType.XbarR);
        persisted.LowerSpecLimit.Should().Be(9.5m);
        persisted.UpperSpecLimit.Should().Be(10.5m);
        persisted.LowerControlLimit.Should().Be(9.8m);
        persisted.UpperControlLimit.Should().Be(10.2m);
        persisted.NominalValue.Should().Be(10m);
        persisted.SampleSize.Should().Be(5);
        result.Code.Should().Be("SPC-DIA");
    }
}
