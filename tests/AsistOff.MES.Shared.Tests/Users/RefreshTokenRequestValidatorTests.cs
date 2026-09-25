using AsistOff.MES.Users.Application.Features.Authentication.Refresh;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Users;

/// <summary>
/// Validator tests for cookie-transport refresh: the body token is optional
/// because the controller merges the refresh cookie when the body carries
/// none; a supplied token must still be non-blank.
/// </summary>
public class RefreshTokenRequestValidatorTests
{
    [Fact]
    public async Task NullToken_IsValid_CookieMaySupplyIt()
    {
        // Arrange
        var validator = new RefreshTokenRequestValidator();

        // Act
        var result = await validator.ValidateAsync(new RefreshTokenRequest(null));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task BlankToken_IsInvalid()
    {
        // Arrange
        var validator = new RefreshTokenRequestValidator();

        // Act
        var result = await validator.ValidateAsync(new RefreshTokenRequest("  "));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RefreshTokenRequest.RefreshToken));
    }

    [Fact]
    public async Task SuppliedToken_IsValid()
    {
        // Arrange
        var validator = new RefreshTokenRequestValidator();

        // Act
        var result = await validator.ValidateAsync(new RefreshTokenRequest("opaque-token"));

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
