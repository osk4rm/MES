using System.Linq.Expressions;
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

    private void SetupRepository(List<Shift> shifts)
    {
        _repository
            .Setup(r => r.BrowseAsync(It.IsAny<Paginator<Shift>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Paginator<Shift> paginator, CancellationToken _) => Apply(shifts, paginator.Filter));

        _repository
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<Shift>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExpressionStarter<Shift> predicate, CancellationToken _) => Apply(shifts, predicate).Count);
    }

    private static IReadOnlyCollection<Shift> Apply(List<Shift> shifts, ExpressionStarter<Shift> predicate)
    {
        Expression<Func<Shift, bool>> filter = predicate;
        return shifts.AsQueryable().Where(filter).ToList();
    }

    [Fact]
    public async Task Handle_NoFilters_ReturnsEverything()
    {
        SetupRepository(
        [
            Shift("S1", "Morning", true),
            Shift("S2", "Night", false)
        ]);

        var result = await new BrowseShiftsRequestHandler(_repository.Object)
            .Handle(new BrowseShiftsRequest(), CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_CodeNameAndActiveFilters_AreCombined()
    {
        SetupRepository(
        [
            Shift("S1", "Morning", true),
            Shift("S2", "Morning", false),
            Shift("X1", "Morning", true),
            Shift("S3", "Night", true)
        ]);

        var request = new BrowseShiftsRequest { Code = "S", Name = "Morning", IsActive = true };

        var result = await new BrowseShiftsRequestHandler(_repository.Object)
            .Handle(request, CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(i => i.Code == "S1");
    }

    [Fact]
    public async Task Handle_IsActiveFilter_ReturnsOnlyInactiveShifts()
    {
        SetupRepository(
        [
            Shift("S1", "Morning", true),
            Shift("S2", "Night", false)
        ]);

        var result = await new BrowseShiftsRequestHandler(_repository.Object)
            .Handle(new BrowseShiftsRequest { IsActive = false }, CancellationToken.None);

        result.Items.Should().ContainSingle(i => i.Code == "S2");
    }

    [Fact]
    public async Task Handle_EmptyRepository_ReturnsEmptyPage()
    {
        SetupRepository([]);

        var result = await new BrowseShiftsRequestHandler(_repository.Object)
            .Handle(new BrowseShiftsRequest { Code = "missing" }, CancellationToken.None);

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
        result.TotalPages.Should().Be(0);
    }

    private static Shift Shift(string code, string name, bool isActive) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Code = code,
        Name = name,
        StartTime = new TimeOnly(6, 0),
        EndTime = new TimeOnly(14, 0),
        IsActive = isActive
    };
}
