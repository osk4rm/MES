using System.Text.Json;
using AsistOff.MES.Shared.Infrastructure.Health;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AsistOff.MES.Shared.Tests.Health;

/// <summary>
/// Verifies the probe response writer: 200 JSON when healthy, 503
/// <c>application/problem+json</c> when not, and never any provider error
/// details (hosts, secrets) in the body — issue #249.
/// </summary>
public class HealthProbeResponseWriterTests
{
    [Fact]
    public async Task WriteResponseAsync_HealthyReport_Returns200Json()
    {
        // Arrange
        var context = HttpContext();
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["self"] = new HealthReportEntry(
                    HealthStatus.Healthy, "Process is running.", TimeSpan.Zero, null,
                    new Dictionary<string, object>())
            },
            TimeSpan.Zero);

        // Act
        await HealthProbeResponseWriter.WriteResponseAsync(context, report);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/json");
        Body(context).Should().Contain("Healthy");
    }

    [Fact]
    public async Task WriteResponseAsync_UnhealthyReport_Returns503ProblemDetails()
    {
        // Arrange
        var context = HttpContext("/health/ready");
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["postgres"] = new HealthReportEntry(
                    HealthStatus.Unhealthy, "PostgreSQL is unavailable.", TimeSpan.Zero, null,
                    new Dictionary<string, object>())
            },
            TimeSpan.Zero);

        // Act
        await HealthProbeResponseWriter.WriteResponseAsync(context, report);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.ContentType.Should().Be("application/problem+json");

        var body = Body(context);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("status").GetInt32().Should().Be(503);
        document.RootElement.GetProperty("title").GetString().Should().NotBeNullOrEmpty();
        document.RootElement.GetProperty("detail").GetString().Should().NotBeNullOrEmpty();
        document.RootElement.GetProperty("instance").GetString().Should().Be("/health/ready");
    }

    [Fact]
    public async Task WriteResponseAsync_EntryCarryingSecrets_DoesNotLeakThem()
    {
        // Arrange - a hostile entry whose exception message contains a
        // connection string; the writer must never serialize it.
        const string secret = "Password=super-secret-249";
        var context = HttpContext("/health/ready");
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["postgres"] = new HealthReportEntry(
                    HealthStatus.Unhealthy, $"connection failed: Host=db;{secret}", TimeSpan.Zero,
                    new InvalidOperationException($"Npgsql failure with {secret}"),
                    new Dictionary<string, object>())
            },
            TimeSpan.Zero);

        // Act
        await HealthProbeResponseWriter.WriteResponseAsync(context, report);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        var body = Body(context);
        body.Should().NotContain(secret);
        body.ToLowerInvariant().Should().NotContain("password");
        body.ToLowerInvariant().Should().NotContain("connectionstring");
    }

    private static DefaultHttpContext HttpContext(string path = "/health/live")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        return context;
    }

    private static string Body(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);

        return reader.ReadToEnd();
    }
}
