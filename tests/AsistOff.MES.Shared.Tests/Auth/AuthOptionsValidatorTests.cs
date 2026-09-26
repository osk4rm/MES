using AsistOff.MES.Shared.Infrastructure.Auth;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Auth;

public class AuthOptionsValidatorTests
{
    private static AuthOptions ValidOptions() => new()
    {
        IssuerSigningKey = new string('k', 40),
        Issuer = "AsistOff.MES",
        ValidIssuer = "AsistOff.MES",
        Audience = "AsistOff.MES.Users",
        ValidAudience = "AsistOff.MES.Users",
        ValidateIssuer = true,
        ValidateAudience = true,
        RequireAudience = true,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ValidateIssuerSigningKey = true
    };

    [Fact]
    public void Validate_ShortKey_ThrowsNamingKey()
    {
        // Arrange
        var options = ValidOptions();
        options.IssuerSigningKey = "too-short";

        // Act
        var act = () => AuthOptionsValidator.Validate(options, isProduction: false);

        // Assert — startup must name the offending setting.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*auth:IssuerSigningKey*256 bits*");
    }

    [Fact]
    public void Validate_KeyExactly32Bytes_Passes()
    {
        // Arrange
        var options = ValidOptions();
        options.IssuerSigningKey = new string('k', 32);

        // Act
        var act = () => AuthOptionsValidator.Validate(options, isProduction: false);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_MissingKey_Throws()
    {
        // Arrange
        var options = ValidOptions();
        options.IssuerSigningKey = "";

        // Act
        var act = () => AuthOptionsValidator.Validate(options, isProduction: false);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Validate_MissingAudienceInProduction_Throws()
    {
        // Arrange
        var options = ValidOptions();
        options.Audience = null;
        options.ValidAudience = null;
        options.ValidAudiences = null;

        // Act
        var act = () => AuthOptionsValidator.Validate(options, isProduction: true);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Audience*Production*");
    }

    [Fact]
    public void Validate_MissingIssuerInProduction_Throws()
    {
        // Arrange
        var options = ValidOptions();
        options.Issuer = null;
        options.ValidIssuer = null;
        options.ValidIssuers = null;

        // Act
        var act = () => AuthOptionsValidator.Validate(options, isProduction: true);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Issuer*Production*");
    }

    [Fact]
    public void Validate_DisabledAudienceValidationInProduction_Throws()
    {
        // Arrange
        var options = ValidOptions();
        options.ValidateAudience = false;

        // Act
        var act = () => AuthOptionsValidator.Validate(options, isProduction: true);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ValidateAudience*");
    }

    [Fact]
    public void Validate_SecureDefaults_AreTrue()
    {
        // Arrange — secure by default: a freshly-bound options object must
        // validate signatures and audience without explicit opt-in.

        // Act
        var options = new AuthOptions { IssuerSigningKey = new string('k', 40) };

        // Assert
        options.ValidateAudience.Should().BeTrue();
        options.RequireAudience.Should().BeTrue();
        options.ValidateIssuerSigningKey.Should().BeTrue();
    }

    [Fact]
    public void Validate_DisabledSigningKeyValidationInProduction_Throws()
    {
        // Arrange
        var options = ValidOptions();
        options.ValidateIssuerSigningKey = false;

        // Act
        var act = () => AuthOptionsValidator.Validate(options, isProduction: true);

        // Assert — accepting unsigned tokens is never legitimate.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ValidateIssuerSigningKey*");
    }

    [Fact]
    public void Validate_DisabledRequireAudienceInProduction_Throws()
    {
        // Arrange — audience validation on but audience not required still
        // admits tokens without an aud claim.
        var options = ValidOptions();
        options.RequireAudience = false;

        // Act
        var act = () => AuthOptionsValidator.Validate(options, isProduction: true);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ValidateAudience*");
    }

    [Fact]
    public void Validate_DisabledLifetimeValidationInProduction_Throws()
    {
        // Arrange
        var options = ValidOptions();
        options.ValidateLifetime = false;

        // Act
        var act = () => AuthOptionsValidator.Validate(options, isProduction: true);

        // Assert — expired tokens must never be accepted in production.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ValidateLifetime*");
    }

    [Fact]
    public void Validate_MissingAudienceOutsideProduction_PassesDevBypass()
    {
        // Arrange — dev-only bypass: audience may be unset outside Production.
        var options = ValidOptions();
        options.Audience = null;
        options.ValidAudience = null;
        options.ValidAudiences = null;
        options.ValidateAudience = false;
        options.RequireAudience = false;

        // Act
        var act = () => AuthOptionsValidator.Validate(options, isProduction: false);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ValidProductionOptions_Passes()
    {
        // Arrange
        var options = ValidOptions();

        // Act
        var act = () => AuthOptionsValidator.Validate(options, isProduction: true);

        // Assert
        act.Should().NotThrow();
    }
}
