using AsistOff.MES.Shared.Infrastructure.Health;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AsistOff.MES.Shared.Tests.Health;

/// <summary>
/// Verifies the tag predicates that split the Gateway probes into liveness
/// (<c>/health/live</c>) and readiness (<c>/health/ready</c> plus the
/// <c>/health</c> alias) — issue #249.
/// </summary>
public class HealthProbesTests
{
    [Fact]
    public void IsLiveCheck_LiveTaggedRegistration_ReturnsTrue()
    {
        // Arrange
        var registration = Registration(tags: new[] { HealthProbes.LiveTag });

        // Act
        var result = HealthProbes.IsLiveCheck(registration);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsLiveCheck_ReadyTaggedRegistration_ReturnsFalse()
    {
        // Arrange - the database check must never run on the liveness probe,
        // otherwise /health/live would fail during a PostgreSQL outage.
        var registration = Registration(tags: new[] { HealthProbes.ReadyTag });

        // Act
        var result = HealthProbes.IsLiveCheck(registration);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsReadyCheck_ReadyTaggedRegistration_ReturnsTrue()
    {
        // Arrange
        var registration = Registration(tags: new[] { HealthProbes.ReadyTag });

        // Act
        var result = HealthProbes.IsReadyCheck(registration);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsReadyCheck_LiveTaggedRegistration_ReturnsFalse()
    {
        // Arrange
        var registration = Registration(tags: new[] { HealthProbes.LiveTag });

        // Act
        var result = HealthProbes.IsReadyCheck(registration);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ProbePaths_LiveReadyAndAlias_AreDistinct()
    {
        // Arrange & Act & Assert — orchestrators configure each path
        // separately, so they must not collide.
        new[] { HealthProbes.LivePath, HealthProbes.ReadyPath, HealthProbes.AliasPath }
            .Should().OnlyHaveUniqueItems();
    }

    private static HealthCheckRegistration Registration(string[] tags)
        => new(HealthProbes.SelfCheckName, new SelfLivenessCheck(), null, tags);
}
