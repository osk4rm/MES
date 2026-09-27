using AsistOff.MES.Gateway.Controllers;
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
/// Startup wiring guard for issue #351 (security audit 2026-09-26, Low):
/// <c>Extensions.AddAuth</c> must register a global fallback authorization
/// policy requiring an authenticated user, so any controller endpoint without
/// explicit auth metadata fails closed with 401 instead of being anonymously
/// reachable. <c>ErrorsController.Error</c> (the exception-handler
/// re-execution path) is the deliberate exception and must keep its explicit
/// <c>[AllowAnonymous]</c>.
/// </summary>
public sealed class FallbackAuthorizationPolicyTests
{
    [Fact]
    public async Task AddAuth_WithModulePolicies_RegistersFallbackPolicyRequiringAuthenticatedUserAsync()
    {
        // Arrange
        var services = CreateServices();
        IList<IModule> modules = [new StubModule("A", ["configuration"])];

        // Act
        services.AddAuth(modules: modules);
        var provider = services.BuildServiceProvider().GetRequiredService<IAuthorizationPolicyProvider>();

        // Assert — the fallback fails closed for anonymous callers.
        var fallback = await provider.GetFallbackPolicyAsync();
        fallback.Should().NotBeNull("endpoints without explicit auth metadata must fail closed");
        fallback!.Requirements.OfType<DenyAnonymousAuthorizationRequirement>()
            .Should().ContainSingle("the fallback policy must require an authenticated user");
    }

    [Fact]
    public async Task AddAuth_WithNullModules_StillRegistersFallbackPolicyAsync()
    {
        // Arrange — the fail-closed default must not depend on module discovery.
        var services = CreateServices();

        // Act
        services.AddAuth(modules: null);
        var provider = services.BuildServiceProvider().GetRequiredService<IAuthorizationPolicyProvider>();

        // Assert
        var fallback = await provider.GetFallbackPolicyAsync();
        fallback.Should().NotBeNull();
        fallback!.Requirements.OfType<DenyAnonymousAuthorizationRequirement>()
            .Should().ContainSingle();
    }

    [Fact]
    public void ErrorsController_Error_CarriesAllowAnonymous()
    {
        // Arrange & Act — reflection guard so a future edit cannot silently
        // close the error path (an auth challenge on /error would mask the
        // real sanitized problem response for anonymous callers).
        var method = typeof(ErrorsController).GetMethod(nameof(ErrorsController.Error));

        // Assert
        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
            .Should().ContainSingle(
                "the exception-handler path must stay reachable without authentication");
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
