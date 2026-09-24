using AsistOff.MES.Configuration.Application.Features.Shifts.Delete;
using AsistOff.MES.Configuration.Application.Features.Shifts.Get;
using AsistOff.MES.Configuration.Application.Features.Shifts.Update;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class UpdateDeleteGetShiftRequestHandlerTests
{
    private readonly Mock<IShiftsRepository> _repository = new();

    private static Shift Shift(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Code = "S1",
        Name = "Morning",
        Description = null,
        StartTime = new TimeOnly(6, 0),
        EndTime = new TimeOnly(14, 0),
        IsActive = true
    };

    [Fact]
    public async Task Get_UnknownId_ThrowsNotFoundException()
    {
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shift?)null);

        var act = () => new GetShiftRequestHandler(_repository.Object)
            .Handle(new GetShiftRequest(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Get_ExistingId_ReturnsMappedShift()
    {
        var shift = Shift();
        _repository
            .Setup(r => r.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shift);

        var result = await new GetShiftRequestHandler(_repository.Object)
            .Handle(new GetShiftRequest(shift.Id), CancellationToken.None);

        result.Id.Should().Be(shift.Id);
        result.Code.Should().Be("S1");
        result.StartTime.Should().Be(new TimeOnly(6, 0));
    }

    [Fact]
    public async Task Update_UnknownId_ThrowsNotFoundException()
    {
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shift?)null);

        var id = Guid.NewGuid();
        var act = () => new UpdateShiftRequestHandler(_repository.Object).Handle(
            new UpdateShiftRequest(id, "S1", "Morning", null, new TimeOnly(6, 0), new TimeOnly(14, 0), true),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Update_DuplicateCode_ThrowsConflictException()
    {
        var shift = Shift();
        _repository
            .Setup(r => r.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shift);
        _repository
            .Setup(r => r.CodeExistsAsync("S2", shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => new UpdateShiftRequestHandler(_repository.Object).Handle(
            new UpdateShiftRequest(shift.Id, "S2", "Evening", null, new TimeOnly(14, 0), new TimeOnly(22, 0), true),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Update_EmptyCode_ThrowsValidationException()
    {
        var shift = Shift();
        _repository
            .Setup(r => r.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shift);

        var act = () => new UpdateShiftRequestHandler(_repository.Object).Handle(
            new UpdateShiftRequest(shift.Id, "  ", "Evening", null, new TimeOnly(14, 0), new TimeOnly(22, 0), true),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Update_ValidRequest_PersistsChanges()
    {
        var shift = Shift();
        _repository
            .Setup(r => r.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shift);
        _repository
            .Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await new UpdateShiftRequestHandler(_repository.Object).Handle(
            new UpdateShiftRequest(shift.Id, "S1", "Evening", "14-22", new TimeOnly(14, 0), new TimeOnly(22, 0), false),
            CancellationToken.None);

        shift.Name.Should().Be("Evening");
        shift.IsActive.Should().BeFalse();
        _repository.Verify(r => r.UpdateAsync(shift, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_UnknownId_ThrowsNotFoundException()
    {
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shift?)null);

        var act = () => new DeleteShiftRequestHandler(_repository.Object)
            .Handle(new DeleteShiftRequest(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_ExistingId_DeletesById()
    {
        var shift = Shift();
        _repository
            .Setup(r => r.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shift);

        await new DeleteShiftRequestHandler(_repository.Object)
            .Handle(new DeleteShiftRequest(shift.Id), CancellationToken.None);

        _repository.Verify(r => r.DeleteAsync(shift.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
