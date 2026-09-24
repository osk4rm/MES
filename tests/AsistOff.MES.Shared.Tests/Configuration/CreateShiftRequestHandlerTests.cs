using AsistOff.MES.Configuration.Application.Features.Shifts.Create;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class CreateShiftRequestHandlerTests
{
    private readonly Mock<IShiftsRepository> _repository = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<ITenantContext> _tenant = new();

    public CreateShiftRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
        _repository
            .Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private CreateShiftRequestHandler CreateSut() =>
        new(_repository.Object, _guids.Object, _tenant.Object);

    [Fact]
    public async Task Handle_EmptyCode_ThrowsValidationException()
    {
        var request = new CreateShiftRequest("", "Morning", null, Time(6), Time(14), true);

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EmptyName_ThrowsValidationException()
    {
        var request = new CreateShiftRequest("S1", "  ", null, Time(6), Time(14), true);

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_DuplicateCode_ThrowsConflictException()
    {
        _repository
            .Setup(r => r.CodeExistsAsync("S1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreateShiftRequest("S1", "Morning", null, Time(6), Time(14), true);

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsTenantIdAndTimes()
    {
        var tenantId = Guid.NewGuid();
        _tenant.SetupGet(t => t.TenantId).Returns(tenantId);

        Shift? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Shift>(), It.IsAny<CancellationToken>()))
            .Callback<Shift, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((Shift entity, CancellationToken _) => entity);

        var request = new CreateShiftRequest("S1", "Morning", "06-14", Time(6), Time(14), true);

        var result = await CreateSut().Handle(request, CancellationToken.None);

        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(tenantId);
        persisted.Code.Should().Be("S1");
        persisted.StartTime.Should().Be(Time(6));
        persisted.EndTime.Should().Be(Time(14));
        result.StartTime.Should().Be(Time(6));
    }

    [Fact]
    public async Task Handle_OvernightWindow_IsAccepted()
    {
        Shift? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Shift>(), It.IsAny<CancellationToken>()))
            .Callback<Shift, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((Shift entity, CancellationToken _) => entity);

        var request = new CreateShiftRequest("N1", "Night", null, Time(22), Time(6), true);

        await CreateSut().Handle(request, CancellationToken.None);

        persisted!.StartTime.Should().Be(Time(22));
        persisted.EndTime.Should().Be(Time(6));
    }

    private static TimeOnly Time(int hour) => new(hour, 0);
}
