using AsistOff.MES.Multitenancy.Behaviors;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Multitenancy;

public class TenantValidationBehaviorTests
{
    private sealed record PlainRequest : IRequest<string>;
    private sealed record AnonymousRequest : IRequest<string>, IAllowAnonymousRequest;

    [Fact]
    public async Task Should_pass_through_when_tenant_is_present()
    {
        var accessor = new Mock<ICurrentTenantAccessor>();
        var tenantId = Guid.NewGuid();
        accessor.Setup(a => a.TryGetTenantId(out tenantId)).Returns(true);

        var behavior = new TenantValidationBehavior<PlainRequest, string>(
            accessor.Object,
            NullLogger<TenantValidationBehavior<PlainRequest, string>>.Instance);

        var result = await behavior.Handle(
            new PlainRequest(),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Should_throw_when_tenant_is_missing_for_non_anonymous_request()
    {
        var accessor = new Mock<ICurrentTenantAccessor>();
        Guid empty = Guid.Empty;
        accessor.Setup(a => a.TryGetTenantId(out empty)).Returns(false);

        var behavior = new TenantValidationBehavior<PlainRequest, string>(
            accessor.Object,
            NullLogger<TenantValidationBehavior<PlainRequest, string>>.Instance);

        var act = () => behavior.Handle(
            new PlainRequest(),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("No valid tenant found for this request");
    }

    [Fact]
    public async Task Should_pass_through_anonymous_request_even_without_tenant()
    {
        var accessor = new Mock<ICurrentTenantAccessor>();
        Guid empty = Guid.Empty;
        accessor.Setup(a => a.TryGetTenantId(out empty)).Returns(false);

        var behavior = new TenantValidationBehavior<AnonymousRequest, string>(
            accessor.Object,
            NullLogger<TenantValidationBehavior<AnonymousRequest, string>>.Instance);

        var result = await behavior.Handle(
            new AnonymousRequest(),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        result.Should().Be("ok");
    }
}
