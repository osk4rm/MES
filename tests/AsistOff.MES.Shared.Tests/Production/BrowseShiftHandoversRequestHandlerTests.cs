using AsistOff.MES.Production.Application.Features.ShiftHandovers.Browse;
using AsistOff.MES.Production.Application.Features.ShiftHandovers.Get;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class BrowseShiftHandoversRequestHandlerTests
{
    private readonly Mock<IShiftHandoversRepository> _handovers = new();

    private BrowseShiftHandoversRequestHandler CreateSut() => new(_handovers.Object);

    private static ShiftHandover MakeEntry(Guid machineId, DateTime from, Guid? shiftId = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = machineId,
        ShiftId = shiftId,
        From = from,
        To = from.AddHours(8),
        Notes = $"Handover {from:o}",
        OpenOrdersCount = 2,
        ActiveAndonCount = 1,
        CreatedAt = from.AddHours(8)
    };

    private void Seed(params ShiftHandover[] entries)
    {
        _handovers.Setup(h => h.BrowseAsync(It.IsAny<Paginator<ShiftHandover>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries.ToList());
        _handovers.Setup(h => h.CountAsync(It.IsAny<LinqKit.ExpressionStarter<ShiftHandover>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries.Length);
    }

    [Fact]
    public async Task Handle_Entries_ReturnsNewestFirst()
    {
        // Arrange — reverse-alphabetical insertion would hide an ordering bug,
        // so seed oldest-first and expect newest-first out.
        var machineId = Guid.NewGuid();
        var oldest = MakeEntry(machineId, new DateTime(2026, 9, 19, 6, 0, 0, DateTimeKind.Utc));
        var newest = MakeEntry(machineId, new DateTime(2026, 9, 21, 6, 0, 0, DateTimeKind.Utc));
        var middle = MakeEntry(machineId, new DateTime(2026, 9, 20, 6, 0, 0, DateTimeKind.Utc));
        Seed(oldest, newest, middle);

        // Act
        var result = await CreateSut().Handle(new BrowseShiftHandoversRequest(), CancellationToken.None);

        // Assert
        result.Items.Select(i => i.Id).Should().ContainInOrder(newest.Id, middle.Id, oldest.Id);
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_MachineFilter_ExcludesOtherMachines()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        Seed(MakeEntry(machineId, new DateTime(2026, 9, 21, 6, 0, 0, DateTimeKind.Utc)),
            MakeEntry(Guid.NewGuid(), new DateTime(2026, 9, 21, 6, 0, 0, DateTimeKind.Utc)));

        // Act
        var result = await CreateSut().Handle(
            new BrowseShiftHandoversRequest { MachineId = machineId }, CancellationToken.None);

        // Assert
        result.Items.Should().ContainSingle().Which.MachineId.Should().Be(machineId);
    }

    [Fact]
    public async Task Handle_DateRangeFilter_ExcludesBoundariesOutsideWindow()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        var inside = MakeEntry(machineId, new DateTime(2026, 9, 21, 6, 0, 0, DateTimeKind.Utc));
        var outside = MakeEntry(machineId, new DateTime(2026, 9, 18, 6, 0, 0, DateTimeKind.Utc));
        Seed(inside, outside);

        // Act
        var result = await CreateSut().Handle(
            new BrowseShiftHandoversRequest
            {
                From = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc),
                To = new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc)
            },
            CancellationToken.None);

        // Assert
        result.Items.Should().ContainSingle().Which.Id.Should().Be(inside.Id);
    }

    [Fact]
    public async Task Handle_NullShiftId_MapsToUncoveredShift()
    {
        // Arrange
        var shiftId = Guid.NewGuid();
        Seed(MakeEntry(Guid.NewGuid(), new DateTime(2026, 9, 21, 6, 0, 0, DateTimeKind.Utc)),
            MakeEntry(Guid.NewGuid(), new DateTime(2026, 9, 21, 14, 0, 0, DateTimeKind.Utc), shiftId));

        // Act
        var result = await CreateSut().Handle(new BrowseShiftHandoversRequest(), CancellationToken.None);

        // Assert
        result.Items.Should().ContainSingle(i => i.UncoveredShift).Which.ShiftId.Should().BeNull();
        result.Items.Should().ContainSingle(i => !i.UncoveredShift).Which.ShiftId.Should().Be(shiftId);
    }

    [Fact]
    public async Task Get_ById_ReturnsEntryWithSnapshotCounts()
    {
        // Arrange
        var entry = MakeEntry(Guid.NewGuid(), new DateTime(2026, 9, 21, 6, 0, 0, DateTimeKind.Utc), Guid.NewGuid());
        _handovers.Setup(h => h.GetAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        var sut = new GetShiftHandoverByIdRequestHandler(_handovers.Object);

        // Act
        var result = await sut.Handle(new GetShiftHandoverByIdRequest(entry.Id), CancellationToken.None);

        // Assert
        result.Id.Should().Be(entry.Id);
        result.Notes.Should().Be(entry.Notes);
        result.MachineId.Should().Be(entry.MachineId);
        result.ShiftId.Should().Be(entry.ShiftId);
        result.UncoveredShift.Should().BeFalse();
        result.OpenOrdersCount.Should().Be(2);
        result.ActiveAndonCount.Should().Be(1);
    }

    [Fact]
    public async Task Get_UnknownId_ThrowsNotFoundException()
    {
        // Arrange
        _handovers.Setup(h => h.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShiftHandover?)null);
        var sut = new GetShiftHandoverByIdRequestHandler(_handovers.Object);

        // Act
        var act = () => sut.Handle(new GetShiftHandoverByIdRequest(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task BrowseValidator_PageSizeOver100_IsInvalid()
    {
        // Arrange
        var validator = new BrowseShiftHandoversValidator();

        // Act
        var result = await validator.ValidateAsync(
            new BrowseShiftHandoversRequest { PageSize = 101 });

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task BrowseValidator_ReversedRange_IsInvalid()
    {
        // Arrange
        var validator = new BrowseShiftHandoversValidator();

        // Act
        var result = await validator.ValidateAsync(
            new BrowseShiftHandoversRequest
            {
                From = new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc),
                To = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc)
            });

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task BrowseValidator_DefaultPaging_IsValid()
    {
        // Arrange
        var validator = new BrowseShiftHandoversValidator();

        // Act
        var result = await validator.ValidateAsync(new BrowseShiftHandoversRequest());

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DefaultSortString_ParsesAgainstQueryable_NewestFirst()
    {
        // Arrange — the handler defaults RawSort to ["From,desc"]; prove the
        // dynamic-LINQ sort string resolves against the entity (guards the
        // real EF query path exercised by the endpoint tests in CI).
        var machineId = Guid.NewGuid();
        var rows = new[]
        {
            MakeEntry(machineId, new DateTime(2026, 9, 19, 6, 0, 0, DateTimeKind.Utc)),
            MakeEntry(machineId, new DateTime(2026, 9, 21, 6, 0, 0, DateTimeKind.Utc)),
            MakeEntry(machineId, new DateTime(2026, 9, 20, 6, 0, 0, DateTimeKind.Utc))
        }.AsQueryable();
        var request = new BrowseShiftHandoversRequest { RawSort = ["From,desc"] };
        var paginator = new Paginator<ShiftHandover>(
            LinqKit.PredicateBuilder.New<ShiftHandover>(true), request);

        // Act
        var result = rows.PageFilter(paginator).ToList();

        // Assert
        result.Select(x => x.From).Should().BeInDescendingOrder();
    }
}
