using AsistOff.MES.Shared.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

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

    [Theory]
    [InlineData("Staging")]
    [InlineData("Test")]
    [InlineData("Production")]
    public void AddAuth_WithAuthenticationDisabled_OutsideDevelopment_ThrowsFailClosed(string environmentName)
    {
        // Arrange — issue #332: the bypass is explicit Development use only.
        var services = CreateServices(authenticationDisabled: true);
        var environment = new StubHostEnvironment(environmentName);

        // Act
        var act = () => services.AddAuth(environment);

        // Assert — Staging/Test/Production refuse to boot instead of serving
        // unauthenticated traffic.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*Development*'{environmentName}'*");
    }

    [Fact]
    public void AddAuth_WithAuthenticationDisabled_InDevelopment_RegistersBypass()
    {
        // Arrange
        var services = CreateServices(authenticationDisabled: true);

        // Act
        var act = () => services.AddAuth(new StubHostEnvironment("Development"));

        // Assert — Development boots with the bypass plus the startup warning.
        // (The evaluator type is internal to Shared.Infrastructure, so assert
        // by name — Shared.Tests covers the same registration by type.)
        act.Should().NotThrow();
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IPolicyEvaluator>()
            .GetType().Name.Should().Be("DisabledAuthenticationPolicyEvaluator");
        provider.GetServices<IHostedService>()
            .Should().ContainSingle(s => s is AuthenticationDisabledWarningService);
    }

    [Fact]
    public void AddAuth_WithAuthenticationEnabled_InStaging_RegistersNoBypass()
    {
        // Arrange — flag off: JWT + RBAC enforced, behavior unchanged.
        var services = CreateServices(authenticationDisabled: false);

        // Act
        var act = () => services.AddAuth(new StubHostEnvironment("Staging"));

        // Assert
        act.Should().NotThrow();
        services.BuildServiceProvider()
            .GetRequiredService<IPolicyEvaluator>()
            .GetType().Name.Should().NotBe("DisabledAuthenticationPolicyEvaluator");
    }

    private static ServiceCollection CreateServices(bool authenticationDisabled)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["auth:IssuerSigningKey"] = new string('k', 40),
                ["auth:Issuer"] = "AsistOff.MES",
                ["auth:Audience"] = "AsistOff.MES",
                ["auth:AuthenticationDisabled"] = authenticationDisabled ? "true" : "false",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        return services;
    }

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "AsistOff.MES.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
