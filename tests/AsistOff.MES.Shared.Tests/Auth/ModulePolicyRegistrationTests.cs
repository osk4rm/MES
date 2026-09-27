using AsistOff.MES.Shared.Abstractions.Modules;
using AsistOff.MES.Shared.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Shared.Tests.Auth;

/// <summary>
/// Startup wiring guard for issue #329: <c>Extensions.AddAuth</c> must register
/// every policy declared by every discovered <see cref="IModule.Policies"/> entry
/// as an MVC authorization policy, while null/empty inputs register nothing and
/// duplicate policy names across modules do not throw.
/// </summary>
public sealed class ModulePolicyRegistrationTests
{
    [Fact]
    public async Task AddAuth_WithModulePolicies_RegistersEachPolicyAsync()
    {
        // Arrange
        var services = CreateServices();
        IList<IModule> modules = [new StubModule("A", ["configuration", "production"])];

        // Act
        services.AddAuth(modules: modules);
        var provider = services.BuildServiceProvider().GetRequiredService<IAuthorizationPolicyProvider>();

        // Assert — each declared policy resolves and requires the matching permissions claim.
        foreach (var policyName in new[] { "configuration", "production" })
        {
            var policy = await provider.GetPolicyAsync(policyName);
            policy.Should().NotBeNull($"module policy '{policyName}' must be registered at startup");
            policy!.Requirements.OfType<ClaimsAuthorizationRequirement>()
                .Should().ContainSingle(r =>
                    r.ClaimType == "permissions"
                    && r.AllowedValues != null
                    && r.AllowedValues.Contains(policyName));
        }
    }

    [Fact]
    public async Task AddAuth_WithNullModules_RegistersNothingAndDoesNotThrowAsync()
    {
        // Arrange
        var services = CreateServices();

        // Act
        var act = () => services.AddAuth(modules: null);

        // Assert — no exception, and no module policy is registered.
        act.Should().NotThrow();
        var provider = services.BuildServiceProvider().GetRequiredService<IAuthorizationPolicyProvider>();
        (await provider.GetPolicyAsync("configuration")).Should().BeNull();
    }

    [Fact]
    public async Task AddAuth_WithEmptyModules_RegistersNothingAndDoesNotThrowAsync()
    {
        // Arrange
        var services = CreateServices();

        // Act
        var act = () => services.AddAuth(modules: []);

        // Assert
        act.Should().NotThrow();
        var provider = services.BuildServiceProvider().GetRequiredService<IAuthorizationPolicyProvider>();
        (await provider.GetPolicyAsync("configuration")).Should().BeNull();
    }

    [Fact]
    public async Task AddAuth_WithDuplicatePolicyNames_DoesNotThrowAndResolvesOnceAsync()
    {
        // Arrange — two modules declaring the same policy name must not trip
        // AuthorizationOptions.AddPolicy's duplicate-name guard.
        var services = CreateServices();
        IList<IModule> modules =
        [
            new StubModule("A", ["configuration"]),
            new StubModule("B", ["configuration", "production"]),
        ];

        // Act
        var act = () => services.AddAuth(modules: modules);

        // Assert
        act.Should().NotThrow();
        var provider = services.BuildServiceProvider().GetRequiredService<IAuthorizationPolicyProvider>();
        (await provider.GetPolicyAsync("configuration")).Should().NotBeNull();
        (await provider.GetPolicyAsync("production")).Should().NotBeNull();
    }

    [Fact]
    public async Task AddAuth_UnknownPolicy_StillFailsAsync()
    {
        // Arrange
        var services = CreateServices();
        IList<IModule> modules = [new StubModule("A", ["configuration"])];

        // Act
        services.AddAuth(modules: modules);
        var provider = services.BuildServiceProvider().GetRequiredService<IAuthorizationPolicyProvider>();

        // Assert — undeclared policies still fail closed (null from the provider).
        (await provider.GetPolicyAsync("no-such-policy")).Should().BeNull();
    }

    private static ServiceCollection CreateServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["auth:IssuerSigningKey"] = new string('k', 40),
                ["auth:Issuer"] = "AsistOff.MES",
                ["auth:Audience"] = "AsistOff.MES",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        return services;
    }

    private sealed class StubModule : IModule
    {
        public StubModule(string name, IEnumerable<string> policies)
        {
            Name = name;
            Policies = policies;
        }

        public string Name { get; }
        public string Path => Name.ToLowerInvariant();
        public IEnumerable<string> Policies { get; }

        public void Register(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void Use(IApplicationBuilder app)
        {
        }
    }
}
