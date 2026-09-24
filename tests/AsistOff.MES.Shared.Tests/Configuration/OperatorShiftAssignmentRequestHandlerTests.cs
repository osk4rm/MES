using System.Linq.Expressions;
using AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Browse;
using AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Create;
using AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Delete;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class OperatorShiftAssignmentRequestHandlerTests
{
    private readonly Mock<IOperatorShiftAssignmentsRepository> _repository = new();
    private readonly Mock<IOperatorsRepository> _operators = new();
    private readonly Mock<IShiftsRepository> _shifts = new();
    private readonly Mock<IGuidProvider> _guids;
    private readonly Mock<ITenantContext> _tenant;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _operatorId = Guid.NewGuid();
    private readonly Guid _shiftId = Guid.NewGuid();

    public OperatorShiftAssignmentRequestHandlerTests()
    {
        _guids = new Mock<IGuidProvider>();
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _tenant = new Mock<ITenantContext>();
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);

        _operators
            .Setup(r => r.GetByIdAsync(_operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Operator(_operatorId));
        _shifts
            .Setup(r => r.GetByIdAsync(_shiftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Shift(_shiftId));
        _repository
            .Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private CreateOperatorShiftAssignmentRequestHandler CreateSut() =>
        new(_repository.Object, _operators.Object, _shifts.Object, _guids.Object, _tenant.Object);

    [Fact]
    public async Task Handle_ValidRequest_PersistsAssignmentAndReturnsResponse()
    {
        // Arrange
        OperatorShiftAssignment? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<OperatorShiftAssignment>(), It.IsAny<CancellationToken>()))
            .Callback<OperatorShiftAssignment, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((OperatorShiftAssignment entity, CancellationToken _) => entity);

        var date = new DateOnly(2026, 9, 24);
        var request = new CreateOperatorShiftAssignmentRequest(_operatorId, _shiftId, date, "Night cover");

        // Act
        var result = await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(_tenantId);
        persisted.OperatorId.Should().Be(_operatorId);
        persisted.ShiftId.Should().Be(_shiftId);
        persisted.Date.Should().Be(date);
        persisted.Notes.Should().Be("Night cover");
        result.OperatorId.Should().Be(_operatorId);
        result.ShiftId.Should().Be(_shiftId);
        result.Date.Should().Be(date);
    }

    [Fact]
    public async Task Handle_MissingDate_ThrowsValidationException()
    {
        // Arrange
        var request = new CreateOperatorShiftAssignmentRequest(_operatorId, _shiftId, null, null);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_UnknownOperator_ThrowsNotFoundException()
    {
        // Arrange
        var unknownId = Guid.NewGuid();
        _operators
            .Setup(r => r.GetByIdAsync(unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Operator?)null);

        var request = new CreateOperatorShiftAssignmentRequest(unknownId, _shiftId, new DateOnly(2026, 9, 24), null);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_UnknownShift_ThrowsNotFoundException()
    {
        // Arrange
        var unknownId = Guid.NewGuid();
        _shifts
            .Setup(r => r.GetByIdAsync(unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shift?)null);

        var request = new CreateOperatorShiftAssignmentRequest(_operatorId, unknownId, new DateOnly(2026, 9, 24), null);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_DuplicateAssignment_ThrowsConflictException()
    {
        // Arrange
        var date = new DateOnly(2026, 9, 24);
        _repository
            .Setup(r => r.ExistsAsync(_operatorId, _shiftId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreateOperatorShiftAssignmentRequest(_operatorId, _shiftId, date, null);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_DeleteUnknownId_ThrowsNotFoundException()
    {
        // Arrange
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperatorShiftAssignment?)null);

        var handler = new DeleteOperatorShiftAssignmentRequestHandler(_repository.Object);

        // Act
        var act = () => handler.Handle(new DeleteOperatorShiftAssignmentRequest(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_BrowseFiltersByDateRangeOperatorAndShift()
    {
        // Arrange
        var opA = Guid.NewGuid();
        var opB = Guid.NewGuid();
        var shiftA = Guid.NewGuid();
        var shiftB = Guid.NewGuid();
        var assignments = new List<OperatorShiftAssignment>
        {
            Assignment(opA, shiftA, new DateOnly(2026, 9, 24)),
            Assignment(opA, shiftB, new DateOnly(2026, 9, 25)),
            Assignment(opB, shiftA, new DateOnly(2026, 9, 26))
        };
        var repository = new Mock<IOperatorShiftAssignmentsRepository>();
        repository
            .Setup(r => r.BrowseAsync(It.IsAny<Paginator<OperatorShiftAssignment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Paginator<OperatorShiftAssignment> paginator, CancellationToken _) => Apply(assignments, paginator.Filter));
        repository
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<OperatorShiftAssignment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExpressionStarter<OperatorShiftAssignment> predicate, CancellationToken _) => Apply(assignments, predicate).Count);

        var handler = new BrowseOperatorShiftAssignmentsRequestHandler(repository.Object);

        // Act
        var byOperator = await handler.Handle(
            new BrowseOperatorShiftAssignmentsRequest { OperatorId = opA }, CancellationToken.None);
        var byShift = await handler.Handle(
            new BrowseOperatorShiftAssignmentsRequest { ShiftId = shiftA }, CancellationToken.None);
        var byRange = await handler.Handle(
            new BrowseOperatorShiftAssignmentsRequest
            {
                DateFrom = new DateOnly(2026, 9, 25),
                DateTo = new DateOnly(2026, 9, 26)
            }, CancellationToken.None);
        var byExactDate = await handler.Handle(
            new BrowseOperatorShiftAssignmentsRequest { Date = new DateOnly(2026, 9, 24) }, CancellationToken.None);

        // Assert
        byOperator.TotalCount.Should().Be(2);
        byShift.TotalCount.Should().Be(2);
        byRange.TotalCount.Should().Be(2);
        byExactDate.Items.Should().ContainSingle(i => i.OperatorId == opA && i.ShiftId == shiftA);
    }

    private static IReadOnlyCollection<OperatorShiftAssignment> Apply(
        List<OperatorShiftAssignment> source, ExpressionStarter<OperatorShiftAssignment> predicate)
    {
        Expression<Func<OperatorShiftAssignment, bool>> filter = predicate;
        return source.AsQueryable().Where(filter).ToList();
    }

    private static OperatorShiftAssignment Assignment(Guid operatorId, Guid shiftId, DateOnly date) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        OperatorId = operatorId,
        ShiftId = shiftId,
        Date = date
    };

    private static Operator Operator(Guid id) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        Identifier = "OP-1",
        FirstName = "Jan",
        LastName = "Kowalski",
        RatePerHour = 10m,
        UserId = Guid.Empty
    };

    private static Shift Shift(Guid id) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        Code = "S1",
        Name = "Morning",
        StartTime = new TimeOnly(6, 0),
        EndTime = new TimeOnly(14, 0),
        IsActive = true
    };
}
