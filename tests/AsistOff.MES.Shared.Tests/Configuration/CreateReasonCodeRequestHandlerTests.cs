using AsistOff.MES.Configuration.Application.Features.ReasonCodes.Create;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class CreateReasonCodeRequestHandlerTests
{
    private readonly Mock<IReasonCodesRepository> _repository = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<ITenantContext> _tenant = new();

    public CreateReasonCodeRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
        _repository
            .Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private CreateReasonCodeRequestHandler CreateSut() =>
        new(_repository.Object, _guids.Object, _tenant.Object);

    [Fact]
    public async Task Handle_EmptyCode_ThrowsValidationException()
    {
        var request = new CreateReasonCodeRequest("", "Breakdown", null, ReasonCodeCategory.Downtime, true, 0);

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EmptyName_ThrowsValidationException()
    {
        var request = new CreateReasonCodeRequest("DT-1", "  ", null, ReasonCodeCategory.Downtime, true, 0);

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_DuplicateCode_ThrowsConflictException()
    {
        _repository
            .Setup(r => r.CodeExistsAsync("DT-1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreateReasonCodeRequest("DT-1", "Breakdown", null, ReasonCodeCategory.Downtime, true, 0);
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsCategoryAndTenantId()
    {
        var tenantId = Guid.NewGuid();
        _tenant.SetupGet(t => t.TenantId).Returns(tenantId);

        ReasonCode? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<ReasonCode>(), It.IsAny<CancellationToken>()))
            .Callback<ReasonCode, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((ReasonCode entity, CancellationToken _) => entity);

        var request = new CreateReasonCodeRequest(
            "DT-1", "Breakdown", "Machine failure", ReasonCodeCategory.Downtime, true, 5);

        var result = await CreateSut().Handle(request, CancellationToken.None);

        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(tenantId);
        persisted.Category.Should().Be(ReasonCodeCategory.Downtime);
        persisted.Code.Should().Be("DT-1");
        persisted.SortIndex.Should().Be(5);
        result.Category.Should().Be(ReasonCodeCategory.Downtime);
    }
}
