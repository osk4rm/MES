using AsistOff.MES.Configuration.Application.Features.Shifts.Browse;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class BrowseShiftsRequestHandlerTests
{
    private readonly Mock<IShiftsRepository> _repository = new();

    private BrowseShiftsRequestHandler CreateSut() => new(_repository.Object);

    private Func<Shift, bool> CaptureFilter()
    {
        Paginator<Shift>? captured = null;

        _repository
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Shift>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.BrowseAsync(It.IsAny<Paginator<Shift>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<Shift>, CancellationToken>((paginator, _) => captured = paginator)
            .ReturnsAsync(Array.Empty<Shift>());

        return _ =>
        {
            captured.Should().NotBeNull();
            return captured!.Filter.Compile()(_);
        };
    }

    [Fact]
    public async Task Handle_CodeFilter_OnlyMatchesRequestedCode()
    {
        // Arrange
        var matches = CaptureFilter();
        var request = new BrowseShiftsRequest { Code = "N1" };

        // Act
        await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        matches(new Shift { Code = "N1", Name = "Night", StartTime = new TimeOnly(22, 0), EndTime = new TimeOnly(6, 0) })
            .Should().BeTrue();
        matches(new Shift { Code = "S1", Name = "Morning", StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(14, 0) })
            .Should().BeFalse();
    }

    [Fact]
    public async Task Handle_IsActiveFilter_OnlyMatchesRequestedState()
    {
        // Arrange
        var matches = CaptureFilter();
        var request = new BrowseShiftsRequest { IsActive = false };

        // Act
        await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        matches(new Shift { Code = "S1", Name = "Inactive", IsActive = false })
            .Should().BeTrue();
        matches(new Shift { Code = "S2", Name = "Active", IsActive = true })
            .Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoFilters_MatchesEveryShift()
    {
        // Arrange
        var matches = CaptureFilter();
        var request = new BrowseShiftsRequest();

        // Act
        await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        matches(new Shift { Code = "S1", Name = "Any" })
            .Should().BeTrue();
    }

    [Fact]
    public async Task Handle_RepositoryResults_AreMappedWithTimes()
    {
        // Arrange
        var shifts = new[]
        {
            new Shift { Id = Guid.NewGuid(), Code = "S1", Name = "Morning", StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(14, 0) },
            new Shift { Id = Guid.NewGuid(), Code = "N1", Name = "Night", StartTime = new TimeOnly(22, 0), EndTime = new TimeOnly(6, 0) }
        };
        _repository
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Shift>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _repository
            .Setup(r => r.BrowseAsync(It.IsAny<Paginator<Shift>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(shifts);

        // Act
        var result = await CreateSut().Handle(new BrowseShiftsRequest(), CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().ContainSingle(x => x.Code == "N1" && x.StartTime == new TimeOnly(22, 0));
    }
}
