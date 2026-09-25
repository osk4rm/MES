using AsistOff.MES.Shared.Infrastructure.Health;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AsistOff.MES.Shared.Tests.Health;

/// <summary>
/// Verifies the dependency-free liveness check backing <c>/health/live</c> —
/// issue #249.
/// </summary>
public class SelfLivenessCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_Always_ReturnsHealthy()
    {
        // Arrange
        var check = new SelfLivenessCheck();
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                HealthProbes.SelfCheckName, check, null, new[] { HealthProbes.LiveTag })
        };

        // Act
        var result = await check.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }
}
