using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Application.Features.Permissions.Browse;
using AsistOff.MES.Users.Application.Features.Roles.Assign;
using AsistOff.MES.Users.Application.Features.Roles.Browse;
using AsistOff.MES.Users.Application.Features.Roles.Create;
using AsistOff.MES.Users.Application.Features.Roles.Get;
using AsistOff.MES.Users.Application.Features.Roles.SetPermissions;
using AsistOff.MES.Users.Application.Features.Roles.Unassign;
using AsistOff.MES.Users.Application.Features.Roles.Update;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Users;

/// <summary>
/// Validator tests for the issue #210 role management requests: role create
/// and update validation, permission set replacement input validation, and
/// assignment / unassignment input validation.
/// </summary>
public class RoleManagementValidatorTests
{
    [Fact]
    public async Task CreateRole_EmptyCode_IsInvalid()
    {
        // Arrange
        var validator = new CreateRoleRequestValidator();
        var request = new CreateRoleRequest("  ", "Quality", null);

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.Code));
    }

    [Fact]
    public async Task CreateRole_UppercaseCode_IsInvalid()
    {
        // Arrange
        var validator = new CreateRoleRequestValidator();
        var request = new CreateRoleRequest("Quality-Lead", "Quality lead", null);

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.Code));
    }

    [Fact]
    public async Task CreateRole_EmptyName_IsInvalid()
    {
        // Arrange
        var validator = new CreateRoleRequestValidator();
        var request = new CreateRoleRequest("quality", "  ", null);

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.Name));
    }

    [Fact]
    public async Task CreateRole_ValidRequest_IsValid()
    {
        // Arrange
        var validator = new CreateRoleRequestValidator();
        var request = new CreateRoleRequest("quality.lead-1", "Quality lead", " shift leads ");

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateRole_EmptyName_IsInvalid()
    {
        // Arrange
        var validator = new UpdateRoleRequestValidator();
        var request = new UpdateRoleRequest(Guid.NewGuid(), string.Empty, null);

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.Name));
    }

    [Fact]
    public async Task UpdateRole_ValidRequest_IsValid()
    {
        // Arrange
        var validator = new UpdateRoleRequestValidator();
        var request = new UpdateRoleRequest(Guid.NewGuid(), "Quality lead", null);

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task SetRolePermissions_NullCollection_IsInvalid()
    {
        // Arrange
        var validator = new SetRolePermissionsRequestValidator();
        var request = new SetRolePermissionsRequest(Guid.NewGuid(), null!);

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task SetRolePermissions_EmptyCollection_IsValid()
    {
        // Arrange — clearing every permission is an explicit, valid operation.
        var validator = new SetRolePermissionsRequestValidator();
        var request = new SetRolePermissionsRequest(Guid.NewGuid(), []);

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task AssignUserToRole_EmptyIds_AreInvalid()
    {
        // Arrange
        var validator = new AssignUserToRoleRequestValidator();
        var request = new AssignUserToRoleRequest(Guid.Empty, Guid.Empty);

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.RoleId));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.UserId));
    }

    [Fact]
    public async Task UnassignUserFromRole_EmptyIds_AreInvalid()
    {
        // Arrange
        var validator = new UnassignUserFromRoleRequestValidator();
        var request = new UnassignUserFromRoleRequest(Guid.Empty, Guid.Empty);

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(BrowseRolesRequest))]
    [InlineData(typeof(GetRoleRequest))]
    [InlineData(typeof(CreateRoleRequest))]
    [InlineData(typeof(UpdateRoleRequest))]
    [InlineData(typeof(SetRolePermissionsRequest))]
    [InlineData(typeof(AssignUserToRoleRequest))]
    [InlineData(typeof(UnassignUserFromRoleRequest))]
    [InlineData(typeof(BrowsePermissionsRequest))]
    public void RoleRequests_AreTenantScoped_AndRequireTenantAdmin(Type requestType)
    {
        // Arrange & Act & Assert — every new request is ITenantRequest<T> or
        // ITenantRequest (the generic variant does not extend the non-generic
        // one) and carries RequirePermission("tenant.admin"); no anonymous access.
        var isTenantScoped = typeof(ITenantRequest).IsAssignableFrom(requestType)
            || requestType.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITenantRequest<>));

        isTenantScoped.Should().BeTrue($"{requestType.Name} must be tenant-scoped");

        var permissions = requestType
            .GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
            .Cast<RequirePermissionAttribute>()
            .Select(a => a.Permission)
            .ToList();

        permissions.Should().Contain("tenant.admin");
    }
}
