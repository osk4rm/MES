using System.Linq.Expressions;
using AsistOff.MES.Configuration.Application.Features.OperatorSkills.Assign;
using AsistOff.MES.Configuration.Application.Features.OperatorSkills.Browse;
using AsistOff.MES.Configuration.Application.Features.OperatorSkills.Get;
using AsistOff.MES.Configuration.Application.Features.OperatorSkills.Responses;
using AsistOff.MES.Configuration.Application.Features.OperatorSkills.Unassign;
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

public class OperatorSkillQualificationRequestHandlerTests
{
    private readonly Mock<IOperatorSkillQualificationsRepository> _repository = new();
    private readonly Mock<IOperatorsRepository> _operators = new();
    private readonly Mock<ISkillsRepository> _skills = new();
    private readonly Mock<IGuidProvider> _guids;
    private readonly Mock<ITenantContext> _tenant;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _operatorId = Guid.NewGuid();
    private readonly Guid _skillId = Guid.NewGuid();

    public OperatorSkillQualificationRequestHandlerTests()
    {
        _guids = new Mock<IGuidProvider>();
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _tenant = new Mock<ITenantContext>();
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);

        _operators
            .Setup(r => r.GetByIdAsync(_operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Operator(_operatorId));
        _skills
            .Setup(r => r.GetByIdAsync(_skillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Skill(_skillId, "WELD", "Welding"));
        _repository
            .Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private AssignOperatorSkillRequestHandler CreateSut() =>
        new(_repository.Object, _operators.Object, _skills.Object, _guids.Object, _tenant.Object);

    [Fact]
    public void Requests_AreTenantScoped_AndNotAnonymous()
    {
        // Assert - the qualification matrix must stay tenant-scoped
        typeof(ITenantRequest<OperatorSkillQualificationResponse>)
            .IsAssignableFrom(typeof(AssignOperatorSkillRequest)).Should().BeTrue();
        typeof(ITenantRequest)
            .IsAssignableFrom(typeof(UnassignOperatorSkillRequest)).Should().BeTrue();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(AssignOperatorSkillRequest)).Should().BeFalse();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(UnassignOperatorSkillRequest)).Should().BeFalse();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(BrowseOperatorSkillsRequest)).Should().BeFalse();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(GetOperatorSkillRequest)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsQualificationAndReturnsResponse()
    {
        // Arrange
        OperatorSkillQualification? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<OperatorSkillQualification>(), It.IsAny<CancellationToken>()))
            .Callback<OperatorSkillQualification, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((OperatorSkillQualification entity, CancellationToken _) => entity);

        var request = new AssignOperatorSkillRequest(_operatorId, _skillId);

        // Act
        var result = await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(_tenantId);
        persisted.OperatorId.Should().Be(_operatorId);
        persisted.SkillId.Should().Be(_skillId);
        result.OperatorId.Should().Be(_operatorId);
        result.SkillId.Should().Be(_skillId);
        result.SkillCode.Should().Be("WELD");
        result.SkillName.Should().Be("Welding");
        result.OperatorIdentifier.Should().Be("OP-1");
    }

    [Fact]
    public async Task Handle_DuplicatePair_ThrowsConflictException()
    {
        // Arrange
        _repository
            .Setup(r => r.ExistsAsync(_operatorId, _skillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new AssignOperatorSkillRequest(_operatorId, _skillId);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        _repository.Verify(
            r => r.AddAsync(It.IsAny<OperatorSkillQualification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownOperator_ThrowsNotFoundException()
    {
        // Arrange
        var unknownId = Guid.NewGuid();
        _operators
            .Setup(r => r.GetByIdAsync(unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Operator?)null);

        var request = new AssignOperatorSkillRequest(unknownId, _skillId);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_UnknownSkill_ThrowsNotFoundException()
    {
        // Arrange
        var unknownId = Guid.NewGuid();
        _skills
            .Setup(r => r.GetByIdAsync(unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Skill?)null);

        var request = new AssignOperatorSkillRequest(_operatorId, unknownId);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Handle_EmptyIds_ThrowsValidationException(bool emptyOperator, bool emptySkill)
    {
        // Arrange
        var request = new AssignOperatorSkillRequest(
            emptyOperator ? Guid.Empty : _operatorId,
            emptySkill ? Guid.Empty : _skillId);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_UnassignUnknownId_ThrowsNotFoundException()
    {
        // Arrange
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperatorSkillQualification?)null);

        var handler = new UnassignOperatorSkillRequestHandler(_repository.Object);

        // Act
        var act = () => handler.Handle(new UnassignOperatorSkillRequest(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_UnassignKnownId_DeletesQualification()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Qualification(id, _operatorId, _skillId));

        var handler = new UnassignOperatorSkillRequestHandler(_repository.Object);

        // Act
        await handler.Handle(new UnassignOperatorSkillRequest(id), CancellationToken.None);

        // Assert
        _repository.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_GetUnknownId_ThrowsNotFoundException()
    {
        // Arrange
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperatorSkillQualification?)null);

        var handler = new GetOperatorSkillRequestHandler(_repository.Object);

        // Act
        var act = () => handler.Handle(new GetOperatorSkillRequest(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_BrowseFiltersByOperatorAndSkill()
    {
        // Arrange
        var opA = Guid.NewGuid();
        var opB = Guid.NewGuid();
        var skillA = Guid.NewGuid();
        var skillB = Guid.NewGuid();
        var rows = new List<OperatorSkillQualification>
        {
            Qualification(Guid.NewGuid(), opA, skillA),
            Qualification(Guid.NewGuid(), opA, skillB),
            Qualification(Guid.NewGuid(), opB, skillA)
        };
        var repository = new Mock<IOperatorSkillQualificationsRepository>();
        repository
            .Setup(r => r.BrowseAsync(It.IsAny<Paginator<OperatorSkillQualification>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Paginator<OperatorSkillQualification> paginator, CancellationToken _) => Apply(rows, paginator.Filter));
        repository
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<OperatorSkillQualification>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExpressionStarter<OperatorSkillQualification> predicate, CancellationToken _) => Apply(rows, predicate).Count);

        var handler = new BrowseOperatorSkillsRequestHandler(repository.Object);

        // Act
        var byOperator = await handler.Handle(
            new BrowseOperatorSkillsRequest { OperatorId = opA }, CancellationToken.None);
        var bySkill = await handler.Handle(
            new BrowseOperatorSkillsRequest { SkillId = skillA }, CancellationToken.None);
        var byPair = await handler.Handle(
            new BrowseOperatorSkillsRequest { OperatorId = opB, SkillId = skillB }, CancellationToken.None);

        // Assert
        byOperator.TotalCount.Should().Be(2);
        bySkill.TotalCount.Should().Be(2);
        byPair.TotalCount.Should().Be(0);
        byOperator.Items.Should().OnlyContain(i => i.OperatorId == opA);
        bySkill.Items.Should().OnlyContain(i => i.SkillId == skillA);
    }

    private static IReadOnlyCollection<OperatorSkillQualification> Apply(
        List<OperatorSkillQualification> source, ExpressionStarter<OperatorSkillQualification> predicate)
    {
        Expression<Func<OperatorSkillQualification, bool>> filter = predicate;
        return source.AsQueryable().Where(filter).ToList();
    }

    private static OperatorSkillQualification Qualification(Guid id, Guid operatorId, Guid skillId) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        OperatorId = operatorId,
        SkillId = skillId
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

    private static Skill Skill(Guid id, string code, string name) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        Code = code,
        Name = name
    };
}
