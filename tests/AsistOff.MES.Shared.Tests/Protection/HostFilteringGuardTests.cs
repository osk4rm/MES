using AsistOff.MES.Gateway.Protection;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Protection;

/// <summary>
/// Guards the host-header allowlist fail-fast (issue #369): a wildcard
/// <c>AllowedHosts</c> outside Development never boots, while explicit host
/// lists and the Development wildcard pass.
/// </summary>
public class HostFilteringGuardTests
{
    [Theory]
    [InlineData("*")]
    [InlineData("localhost;*")]
    [InlineData("*.example.com")]
    [InlineData("mes.example.com, *")]
    public void Validate_WildcardOutsideDevelopment_Throws(string allowedHosts)
    {
        // Arrange + Act
        var act = () => HostFilteringGuard.Validate(allowedHosts, isDevelopment: false);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AllowedHosts*");
    }

    [Theory]
    [InlineData("*")]
    [InlineData("*.example.com")]
    public void Validate_WildcardInDevelopment_Passes(string allowedHosts)
    {
        // Arrange + Act
        var act = () => HostFilteringGuard.Validate(allowedHosts, isDevelopment: true);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("mes.example.com")]
    [InlineData("mes.example.com;www.mes.example.com")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ExplicitOrMissingHosts_PassesInAnyEnvironment(string? allowedHosts)
    {
        // Arrange + Act
        var dev = () => HostFilteringGuard.Validate(allowedHosts, isDevelopment: true);
        var prod = () => HostFilteringGuard.Validate(allowedHosts, isDevelopment: false);

        // Assert
        dev.Should().NotThrow();
        prod.Should().NotThrow();
    }

    [Theory]
    [InlineData("*", true)]
    [InlineData(" * ", true)]
    [InlineData("localhost;*", true)]
    [InlineData("*.example.com", true)]
    [InlineData("localhost", false)]
    [InlineData("mes.example.com;www.mes.example.com", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsWildcard_ClassifiesEntries(string? allowedHosts, bool expected)
    {
        // Arrange + Act
        var actual = HostFilteringGuard.IsWildcard(allowedHosts);

        // Assert
        actual.Should().Be(expected);
    }
}
