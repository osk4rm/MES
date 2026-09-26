using AsistOff.MES.Shared.Infrastructure.Boot;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Boot;

/// <summary>
/// Unit tests for the hardened container secrets guard (issue #271):
/// production boot fails fast on a missing or weak <c>POSTGRES_PASSWORD</c>
/// / <c>auth:IssuerSigningKey</c>, while generated secrets pass and
/// non-production hosts boot unchanged.
/// </summary>
public sealed class ContainerSecretsValidatorTests
{
    private const string GeneratedPassword = "X7mQ2vL9pR4tY6wN8sK3vB5nM7qW+eT1uI4oP0aS9dF2gH6jK8=";
    private const string GeneratedSigningKey = "k8F3vQ7xL2mZ9tY4bN6wX1sK5vB7nM3qW+eT1uI4oP0aS9dF2gH6jK8xL0==";

    [Fact]
    public void Validate_MissingPostgresPasswordInProduction_ThrowsNamingVariable()
    {
        // Arrange
        string? postgresPassword = null;

        // Act
        var act = () => ContainerSecretsValidator.Validate(postgresPassword, GeneratedSigningKey, isProduction: true);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*POSTGRES_PASSWORD*required*");
    }

    [Fact]
    public void Validate_WeakPostgresPasswordInProduction_Throws()
    {
        // Arrange — the historical compose default must never boot production.
        var weakPasswords = new[] { "root", "ROOT", "password", "admin", "mes", "short1!" };

        // Act + Assert
        foreach (var weak in weakPasswords)
        {
            var act = () => ContainerSecretsValidator.Validate(weak, GeneratedSigningKey, isProduction: true);
            act.Should().Throw<InvalidOperationException>(
                $"password '{weak}' must be rejected in production");
        }
    }

    [Fact]
    public void Validate_MissingSigningKeyInProduction_ThrowsNamingSetting()
    {
        // Arrange
        string? signingKey = "  ";

        // Act
        var act = () => ContainerSecretsValidator.Validate(GeneratedPassword, signingKey, isProduction: true);

        // Assert — same setting name as AuthOptionsValidator so operators get
        // one consistent message whichever guard fires first.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*auth:IssuerSigningKey*");
    }

    [Fact]
    public void Validate_ShortSigningKeyInProduction_ThrowsNamingEntropyFloor()
    {
        // Arrange
        const string shortKey = "too-short";

        // Act
        var act = () => ContainerSecretsValidator.Validate(GeneratedPassword, shortKey, isProduction: true);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*auth:IssuerSigningKey*256 bits*");
    }

    [Fact]
    public void Validate_GeneratedSecretsInProduction_Passes()
    {
        // Arrange — outputs of `openssl rand -base64 32/48` as documented in
        // .env.example and the production runbook.

        // Act
        var act = () => ContainerSecretsValidator.Validate(GeneratedPassword, GeneratedSigningKey, isProduction: true);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WeakSecretsOutsideProduction_NoopsForDevAndTests()
    {
        // Arrange — Development hosts and the integration-test host use their
        // own credentials; the guard must never break them.

        // Act
        var act = () => ContainerSecretsValidator.Validate("root", "", isProduction: false);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidatePostgresPassword_ExactlyMinimumLength_Passes()
    {
        // Arrange
        var password = new string('a', ContainerSecretsValidator.MinimumPostgresPasswordLength) + "9!";

        // Act
        var act = () => ContainerSecretsValidator.ValidatePostgresPassword(password);

        // Assert
        act.Should().NotThrow();
    }
}
