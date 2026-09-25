using AsistOff.MES.Shared.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Auth;

/// <summary>
/// Startup-path test for the signing-key entropy floor (#232): the real
/// <c>AddAuth</c> registration rejects a weak <c>auth:IssuerSigningKey</c> with an
/// error naming the setting, before the host can serve traffic. Runs without
/// Docker — no database or running host is involved.
/// </summary>
public sealed class AuthStartupValidationTests
{
    [Fact]
    public void AddAuth_WithWeakSigningKey_ThrowsNamingKey()
    {
        // Arrange — same shape as the shipped appsettings "auth" section, but
        // with a key decoding to fewer than 256 bits.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["auth:IssuerSigningKey"] = "too-short",
                ["auth:Issuer"] = "AsistOff.MES",
                ["auth:Audience"] = "AsistOff.MES",
                ["auth:ValidateAudience"] = "true",
                ["auth:RequireAudience"] = "true",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        var act = () => services.AddAuth();

        // Assert — startup fails fast and names the offending setting.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*auth:IssuerSigningKey*256 bits*");
    }
}
