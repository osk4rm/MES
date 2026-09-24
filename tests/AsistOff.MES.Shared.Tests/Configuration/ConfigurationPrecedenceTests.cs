using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace AsistOff.MES.Shared.Tests.Configuration;

/// <summary>
/// Guards the configuration precedence fixed for issue #89: environment
/// variables must win over user secrets. <c>Program.cs</c> re-adds the
/// environment-variable source after <c>AddUserSecrets</c> (last source wins),
/// so <c>postgres__connectionString</c> and other <c>__</c>-separated env
/// overrides always take effect. These tests reproduce that source ordering
/// with an in-memory stand-in for the secrets source.
/// </summary>
public sealed class ConfigurationPrecedenceTests
{
    [Fact]
    public void EnvironmentVariable_AddedAfterSecretsSource_Wins()
    {
        // Arrange (in-memory collection stands in for user secrets)
        const string key = "MES_TEST_PRECEDENCE_CONNECTION";
        const string secretsValue = "Host=secrets;";
        const string envValue = "Host=env;";
        Environment.SetEnvironmentVariable(key, envValue);
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { [key] = secretsValue })
                .AddEnvironmentVariables()
                .Build();

            // Act
            var resolved = configuration[key];

            // Assert
            resolved.Should().Be(envValue);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void DoubleUnderscoreEnvVar_BindsToNestedSection_LikePostgresConnectionString()
    {
        // Arrange (mirrors postgres__connectionString -> postgres:connectionString)
        const string prefix = "MES_TEST_NESTED_";
        const string envName = $"{prefix}postgres__connectionString";
        Environment.SetEnvironmentVariable(envName, "Host=env;");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["postgres:connectionString"] = "Host=secrets;",
                })
                .AddEnvironmentVariables(prefix: prefix)
                .Build();

            // Act
            var resolved = configuration["postgres:connectionString"];

            // Assert
            resolved.Should().Be("Host=env;");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, null);
        }
    }
}
