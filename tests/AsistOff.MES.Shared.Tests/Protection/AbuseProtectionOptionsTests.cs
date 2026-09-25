using System.Text;
using AsistOff.MES.Shared.Infrastructure.Protection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace AsistOff.MES.Shared.Tests.Protection;

/// <summary>
/// Verifies the throttle budget configuration: safe defaults for shared
/// shopfloor terminals and correct binding from the <c>RateLimiting</c>
/// configuration section.
/// </summary>
public class AbuseProtectionOptionsTests
{
    [Fact]
    public void Defaults_AreGenerous_AndWindowsAreOneMinute()
    {
        // Arrange + Act
        var options = new AbuseProtectionOptions();

        // Assert
        options.SignIn.PermitLimit.Should().Be(100);
        options.SignIn.WindowSeconds.Should().Be(60);
        options.SignIn.Window.Should().Be(TimeSpan.FromMinutes(1));
        options.TenantCreate.PermitLimit.Should().Be(60);
        options.TenantCreate.WindowSeconds.Should().Be(60);
        options.TenantCreate.Window.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Bind_FromRateLimitingSection_AppliesCustomBudgets()
    {
        // Arrange
        var json = """
            {
              "RateLimiting": {
                "SignIn": { "PermitLimit": 5, "WindowSeconds": 30 },
                "TenantCreate": { "PermitLimit": 3, "WindowSeconds": 120 }
              }
            }
            """;
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .Build();

        // Act
        var options = configuration
            .GetSection(AbuseProtectionOptions.SectionName)
            .Get<AbuseProtectionOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.SignIn.PermitLimit.Should().Be(5);
        options.SignIn.Window.Should().Be(TimeSpan.FromSeconds(30));
        options.TenantCreate.PermitLimit.Should().Be(3);
        options.TenantCreate.Window.Should().Be(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void Bind_MissingSection_FallsBackToDefaults()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes("{}")))
            .Build();

        // Act
        var options = configuration
            .GetSection(AbuseProtectionOptions.SectionName)
            .Get<AbuseProtectionOptions>() ?? new AbuseProtectionOptions();

        // Assert
        options.SignIn.PermitLimit.Should().Be(100);
        options.TenantCreate.PermitLimit.Should().Be(60);
    }
}
