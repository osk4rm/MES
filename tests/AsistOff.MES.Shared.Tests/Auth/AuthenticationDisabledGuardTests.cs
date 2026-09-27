using AsistOff.MES.Shared.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsistOff.MES.Shared.Tests.Auth;

/// <summary>
/// Startup guard for issue #332: <c>auth:AuthenticationDisabled</c> is an
/// explicit local-development escape hatch only. Development boots with the
/// bypass evaluator plus an unmistakable startup warning; every other
/// environment (Staging/Test/Production/unknown) fails fast instead of
/// serving unauthenticated traffic.
/// </summary>
public sealed class AuthenticationDisabledGuardTests
{
    [Fact]
    public void AddAuth_AuthenticationDisabled_InDevelopment_RegistersBypassAndWarningService()
    {
        // Arrange
        var services = CreateServices(authenticationDisabled: true);
        var environment = StubEnvironment("Development");

        // Act
        var act = () => services.AddAuth(environment);

        // Assert — Development boots with the bypass and the warning service.
        act.Should().NotThrow();
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IPolicyEvaluator>()
            .Should().BeOfType<DisabledAuthenticationPolicyEvaluator>();
        provider.GetServices<IHostedService>()
            .Should().ContainSingle(s => s is AuthenticationDisabledWarningService);
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Test")]
    [InlineData("Production")]
    public void AddAuth_AuthenticationDisabled_OutsideDevelopment_ThrowsFailClosed(string environmentName)
    {
        // Arrange
        var services = CreateServices(authenticationDisabled: true);
        var environment = StubEnvironment(environmentName);

        // Act
        var act = () => services.AddAuth(environment);

        // Assert — fail fast instead of serving unauthenticated requests.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*Development*'{environmentName}'*");
    }

    [Fact]
    public void AddAuth_AuthenticationDisabled_WithNullEnvironment_ThrowsFailClosed()
    {
        // Arrange — no host environment is not explicit Development use.
        var services = CreateServices(authenticationDisabled: true);

        // Act
        var act = () => services.AddAuth(hostEnvironment: null);

        // Assert — startup throws before any evaluator is registered.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Development*");
        services.Should().NotContain(d =>
            d.ServiceType == typeof(IPolicyEvaluator)
            && d.ImplementationType == typeof(DisabledAuthenticationPolicyEvaluator),
            "the bypass must never be registered when startup refuses to boot with it");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void AddAuth_AuthenticationEnabled_RegistersNoBypassInAnyEnvironment(string environmentName)
    {
        // Arrange — flag unset/false: JWT + RBAC enforced everywhere.
        var services = CreateServices(authenticationDisabled: false);
        var environment = StubEnvironment(environmentName);

        // Act
        var act = () => services.AddAuth(environment);

        // Assert — behavior unchanged: no bypass, no warning service, no throw.
        act.Should().NotThrow();
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IPolicyEvaluator>()
            .Should().NotBeOfType<DisabledAuthenticationPolicyEvaluator>();
        provider.GetServices<IHostedService>()
            .Should().NotContain(s => s is AuthenticationDisabledWarningService);
    }

    [Fact]
    public async Task WarningService_StartAsync_LogsUnmistakableBypassWarningAsync()
    {
        // Arrange
        var logger = Mock.Of<ILogger<AuthenticationDisabledWarningService>>();
        var service = new AuthenticationDisabledWarningService(logger);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — never enable the bypass silently.
        Mock.Get(logger).Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("AuthenticationDisabled")
                    && v.ToString()!.Contains("BYPASSED")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DisabledEvaluator_AuthenticateAndAuthorize_SucceedWithPerRequestDebugAsync()
    {
        // Arrange — a hand-written capturing logger: Moq cannot proxy
        // ILogger<T> over the internal evaluator type (strong-named
        // DynamicProxyGenAssembly2 would need InternalsVisibleTo).
        var logger = new CapturingLogger<DisabledAuthenticationPolicyEvaluator>();
        var evaluator = new DisabledAuthenticationPolicyEvaluator(logger);
        var context = new DefaultHttpContext();
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        var authenticateResult = await evaluator.AuthenticateAsync(policy, context);

        // Act
        var authorizeResult = await evaluator.AuthorizeAsync(policy, authenticateResult, context, resource: null);

        // Assert — bypass succeeds every check and debugs each request.
        authenticateResult.Succeeded.Should().BeTrue();
        authorizeResult.Succeeded.Should().BeTrue();
        logger.Entries
            .Where(e => e.Level == LogLevel.Debug && e.Message.Contains("bypassed"))
            .Should().HaveCount(2,
                "every bypassed authenticate + authorize check must emit per-request debug");
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

    private static IHostEnvironment StubEnvironment(string environmentName)
    {
        var mock = new Mock<IHostEnvironment>();
        mock.SetupGet(x => x.EnvironmentName).Returns(environmentName);
        mock.SetupGet(x => x.ApplicationName).Returns("AsistOff.MES.Tests");
        mock.SetupGet(x => x.ContentRootPath).Returns(AppContext.BaseDirectory);
        return mock.Object;
    }

    /// <summary>
    /// Minimal capturing <see cref="ILogger{TCategoryName}"/> test double.
    /// </summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
