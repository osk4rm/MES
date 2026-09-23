using AsistOff.MES.Configuration.Application.Features.ReasonCodes.Browse;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class BrowseReasonCodesRequestHandlerTests
{
    private readonly Mock<IReasonCodesRepository> _repository = new();

    private BrowseReasonCodesRequestHandler CreateSut() => new(_repository.Object);

    private Func<ReasonCode, bool> CaptureFilter()
    {
        Paginator<ReasonCode>? captured = null;

        _repository
            .Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<ReasonCode>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.BrowseAsync(It.IsAny<Paginator<ReasonCode>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<ReasonCode>, CancellationToken>((paginator, _) => captured = paginator)
            .ReturnsAsync(Array.Empty<ReasonCode>());

        return _ =>
        {
            captured.Should().NotBeNull();
            return captured!.Filter.Compile()(_);
        };
    }

    [Fact]
    public async Task Handle_CategoryFilter_OnlyMatchesRequestedCategory()
    {
        var matches = CaptureFilter();
        var request = new BrowseReasonCodesRequest { Category = ReasonCodeCategory.Scrap };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(new ReasonCode { Code = "S1", Name = "Scrap", Category = ReasonCodeCategory.Scrap })
            .Should().BeTrue();
        matches(new ReasonCode { Code = "D1", Name = "Downtime", Category = ReasonCodeCategory.Downtime })
            .Should().BeFalse();
    }

    [Fact]
    public async Task Handle_IsActiveFilter_OnlyMatchesRequestedState()
    {
        var matches = CaptureFilter();
        var request = new BrowseReasonCodesRequest { IsActive = false };

        await CreateSut().Handle(request, CancellationToken.None);

        matches(new ReasonCode { Code = "D1", Name = "Inactive", IsActive = false })
            .Should().BeTrue();
        matches(new ReasonCode { Code = "D2", Name = "Active", IsActive = true })
            .Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoFilters_MatchesEveryReasonCode()
    {
        var matches = CaptureFilter();
        var request = new BrowseReasonCodesRequest();

        await CreateSut().Handle(request, CancellationToken.None);

        matches(new ReasonCode { Code = "D1", Name = "Any", Category = ReasonCodeCategory.Other })
            .Should().BeTrue();
    }
}
