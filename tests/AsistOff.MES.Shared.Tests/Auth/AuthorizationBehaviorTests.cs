using AsistOff.MES.Attachments.Application.Features.List;
using AsistOff.MES.Configuration.Application.Features.Products.Common.Responses;
using AsistOff.MES.Configuration.Application.Features.Products.Create;
using AsistOff.MES.Configuration.Application.Features.Products.Update;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Infrastructure.Behaviors;
using AsistOff.MES.Users.Application.Features.Authentication.SignIn;
using AsistOff.MES.Users.Application.Features.Users.Create;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Auth;

public class AuthorizationBehaviorTests
{
    [RequirePermission("configuration.write")]
    private sealed record ProtectedRequest : IRequest<string>;

    [RequirePermission("configuration.write")]
    [RequirePermission("users.write")]
    private sealed record MultiProtectedRequest : IRequest<string>;

    private sealed record OpenRequest : IRequest<string>;

    // Void (fire-and-forget) command: implements the non-generic IRequest, which
    // since MediatR.Contracts 2.x no longer extends IRequest<Unit>. Regression
    // cover for the pipeline silently skipping such requests (tenant.admin role
    // endpoints returned 404 instead of 403 for read-only callers).
    [RequirePermission("tenant.admin")]
    private sealed record ProtectedVoidRequest : IRequest;

    [Fact]
    public async Task Handle_RequestWithoutAttributeOrAllowlist_ThrowsForbiddenException()
    {
        // Arrange — caller holds no permissions at all; the test-local request is
        // in no allowlist and no legacy pass-through assembly, so default-deny
        // (issue #231) rejects it instead of executing.
        var behavior = CreateBehavior<OpenRequest, string>(Array.Empty<string>());
        var nextCalled = false;

        // Act
        var act = () => behavior.Handle(
            new OpenRequest(),
            _ =>
            {
                nextCalled = true;
                return Task.FromResult("ok");
            },
            CancellationToken.None);

        // Assert — 403 without invoking the handler.
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*permission declaration or an allowlist entry*");
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_AllowlistedRequest_PassesThroughRegardlessOfPermissions()
    {
        // Arrange — sign-in is the documented anonymous bootstrap entry: no token
        // exists yet, so no permission claim can be evaluated.
        var behavior = CreateBehavior<SignInRequest, JsonWebToken>(Array.Empty<string>());

        // Act
        var result = await behavior.Handle(
            new SignInRequest("user@example.com", "Passw0rd!"),
            _ => Task.FromResult<JsonWebToken>(null!),
            CancellationToken.None);

        // Assert — allowlisted requests execute unchanged.
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_LegacyPassthroughAssembly_PassesThroughWithoutPermission()
    {
        // Arrange — Attachments requests keep the legacy pass-through until slice 2/2.
        var behavior = CreateBehavior<ListAttachmentsRequest, IReadOnlyCollection<object>>(Array.Empty<string>());

        // Act
        var result = await behavior.Handle(
            new ListAttachmentsRequest("Product", Guid.NewGuid()),
            _ => Task.FromResult<IReadOnlyCollection<object>>(Array.Empty<object>()),
            CancellationToken.None);

        // Assert — unchanged behavior for the not-yet-covered module.
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RequestWithAttributeAndMatchingPermission_CallsNext()
    {
        // Arrange
        var behavior = CreateBehavior<ProtectedRequest, string>(new[] { "configuration.read", "configuration.write" });

        // Act
        var result = await behavior.Handle(
            new ProtectedRequest(),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        // Assert
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_RequestWithAttributeAndMissingPermission_ThrowsForbiddenException()
    {
        // Arrange — read-only caller.
        var behavior = CreateBehavior<ProtectedRequest, string>(new[] { "configuration.read" });
        var nextCalled = false;

        // Act
        var act = () => behavior.Handle(
            new ProtectedRequest(),
            _ =>
            {
                nextCalled = true;
                return Task.FromResult("ok");
            },
            CancellationToken.None);

        // Assert — 403 without invoking the handler.
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*configuration.write*");
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_RequestWithMultipleAttributes_RequiresEveryPermission()
    {
        // Arrange — caller holds only one of the two required permissions.
        var behavior = CreateBehavior<MultiProtectedRequest, string>(new[] { "configuration.write" });

        // Act
        var act = () => behavior.Handle(
            new MultiProtectedRequest(),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*users.write*");
    }

    [Fact]
    public async Task Handle_CreateUserRequest_RequiresUsersWrite()
    {
        // Arrange — the Users write handler carries RequirePermission("users.write").
        var behavior = CreateBehavior<CreateUserRequest, Unit>(Array.Empty<string>());

        // Act
        var act = () => behavior.Handle(
            new CreateUserRequest("user@example.com", "Passw0rd!", "First", "Last", "Passw0rd!"),
            _ => Task.FromResult(Unit.Value),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*users.write*");
    }

    [Fact]
    public async Task Handle_CreateProductRequest_AllowsHolderOfConfigurationWrite()
    {
        // Arrange — the Configuration write handler carries RequirePermission("configuration.write").
        var behavior = CreateBehavior<CreateProductRequest, ProductResponse>(new[] { "configuration.write" });

        // Act
        var result = await behavior.Handle(
            new CreateProductRequest("CODE", "Name", null, null, null, ScanBy.Code, true, null, null),
            _ => Task.FromResult<ProductResponse>(null!),
            CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UpdateProductRequest_WithoutPermission_ThrowsForbiddenException()
    {
        // Arrange — read-only caller; UpdateProduct is a slice-1 write newly
        // covered with RequirePermission("configuration.write").
        var behavior = CreateBehavior<UpdateProductRequest, ProductResponse>(new[] { "configuration.read" });
        var nextCalled = false;

        // Act
        var act = () => behavior.Handle(
            new UpdateProductRequest(Guid.NewGuid(), "CODE", "Name", null, null, null, ScanBy.Code, true, null, null),
            _ =>
            {
                nextCalled = true;
                return Task.FromResult<ProductResponse>(null!);
            },
            CancellationToken.None);

        // Assert — 403 without invoking the handler.
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*configuration.write*");
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_VoidRequestWithAttributeAndMissingPermission_ThrowsForbiddenException()
    {
        // Arrange — read-only caller, void command (TResponse = Unit in the pipeline).
        var behavior = CreateBehavior<ProtectedVoidRequest, Unit>(new[] { "users.read" });
        var nextCalled = false;

        // Act
        var act = () => behavior.Handle(
            new ProtectedVoidRequest(),
            _ =>
            {
                nextCalled = true;
                return Task.FromResult(Unit.Value);
            },
            CancellationToken.None);

        // Assert — 403 without invoking the handler.
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*tenant.admin*");
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_VoidRequestWithAttributeAndMatchingPermission_CallsNext()
    {
        // Arrange — tenant admin caller.
        var behavior = CreateBehavior<ProtectedVoidRequest, Unit>(new[] { "users.read", "tenant.admin" });

        // Act
        var result = await behavior.Handle(
            new ProtectedVoidRequest(),
            _ => Task.FromResult(Unit.Value),
            CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
    }

    private static AuthorizationBehavior<TRequest, TResponse> CreateBehavior<TRequest, TResponse>(
        IReadOnlyCollection<string> permissions)
        where TRequest : IBaseRequest
    {
        var accessor = new Mock<ICurrentPermissionsAccessor>();
        accessor.SetupGet(a => a.Permissions).Returns(permissions);

        return new AuthorizationBehavior<TRequest, TResponse>(
            accessor.Object,
            NullLogger<AuthorizationBehavior<TRequest, TResponse>>.Instance);
    }
}
