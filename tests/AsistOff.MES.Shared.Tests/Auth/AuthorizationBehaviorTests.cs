using AsistOff.MES.Configuration.Application.Features.Products.Common.Responses;
using AsistOff.MES.Configuration.Application.Features.Products.Create;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Infrastructure.Behaviors;
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

    [Fact]
    public async Task Handle_RequestWithoutAttribute_PassesThroughRegardlessOfPermissions()
    {
        // Arrange — caller holds no permissions at all.
        var behavior = CreateBehavior<OpenRequest, string>(Array.Empty<string>());

        // Act
        var result = await behavior.Handle(
            new OpenRequest(),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        // Assert — existing behavior unchanged.
        result.Should().Be("ok");
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

    private static AuthorizationBehavior<TRequest, TResponse> CreateBehavior<TRequest, TResponse>(
        IReadOnlyCollection<string> permissions)
        where TRequest : IRequest<TResponse>
    {
        var accessor = new Mock<ICurrentPermissionsAccessor>();
        accessor.SetupGet(a => a.Permissions).Returns(permissions);

        return new AuthorizationBehavior<TRequest, TResponse>(
            accessor.Object,
            NullLogger<AuthorizationBehavior<TRequest, TResponse>>.Instance);
    }
}
