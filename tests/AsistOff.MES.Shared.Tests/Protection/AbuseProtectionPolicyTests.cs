using AsistOff.MES.Shared.Infrastructure.Protection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Shared.Tests.Protection;

/// <summary>
/// Verifies which requests the abuse-protection limiter throttles and how the
/// per-IP partition key is derived.
/// </summary>
public class AbuseProtectionPolicyTests
{
    [Theory]
    [InlineData("POST", "/api/auth/sign-in", AbuseProtectionPolicy.SignInScope)]
    [InlineData("POST", "/API/AUTH/SIGN-IN", AbuseProtectionPolicy.SignInScope)]
    [InlineData("POST", "/api/tenants", AbuseProtectionPolicy.TenantCreateScope)]
    [InlineData("POST", "/API/TENANTS", AbuseProtectionPolicy.TenantCreateScope)]
    public void MatchScope_BootstrapPost_ReturnsScope(string method, string path, string expected)
    {
        // Arrange
        var request = Request(method, path);

        // Act
        var scope = AbuseProtectionPolicy.MatchScope(request);

        // Assert
        scope.Should().Be(expected);
    }

    [Theory]
    [InlineData("GET", "/api/auth/sign-in")]
    [InlineData("GET", "/api/tenants")]
    [InlineData("GET", "/api/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    [InlineData("POST", "/api/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    [InlineData("POST", "/api/reason-codes")]
    [InlineData("POST", "/api/auth/refresh")]
    [InlineData("POST", "/health")]
    [InlineData("PUT", "/api/tenants")]
    [InlineData("DELETE", "/api/auth/sign-in")]
    public void MatchScope_OtherRequests_ReturnsNull(string method, string path)
    {
        // Arrange
        var request = Request(method, path);

        // Act
        var scope = AbuseProtectionPolicy.MatchScope(request);

        // Assert
        scope.Should().BeNull();
    }

    [Fact]
    public void ResolveClientIp_UsesFirstForwardedEntry()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.7, 198.51.100.2";

        // Act
        var ip = AbuseProtectionPolicy.ResolveClientIp(context);

        // Assert
        ip.Should().Be("203.0.113.7");
    }

    [Fact]
    public void ResolveClientIp_FallsBackToRemoteAddress()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.1.2.3");

        // Act
        var ip = AbuseProtectionPolicy.ResolveClientIp(context);

        // Assert
        ip.Should().Be("10.1.2.3");
    }

    [Fact]
    public void ResolveClientIp_WithoutAnyAddress_ReturnsUnknown()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var ip = AbuseProtectionPolicy.ResolveClientIp(context);

        // Assert
        ip.Should().Be("unknown");
    }

    [Fact]
    public void BuildPartitionKey_IsScopedPerClientIp()
    {
        // Arrange
        var first = new DefaultHttpContext();
        first.Request.Headers["X-Forwarded-For"] = "203.0.113.7";

        var second = new DefaultHttpContext();
        second.Request.Headers["X-Forwarded-For"] = "203.0.113.8";

        // Act
        var keyA = AbuseProtectionPolicy.BuildPartitionKey(first, AbuseProtectionPolicy.SignInScope);
        var keyB = AbuseProtectionPolicy.BuildPartitionKey(first, AbuseProtectionPolicy.TenantCreateScope);
        var keyC = AbuseProtectionPolicy.BuildPartitionKey(second, AbuseProtectionPolicy.SignInScope);

        // Assert
        keyA.Should().Be("signin:203.0.113.7");
        keyB.Should().Be("tenant-signup:203.0.113.7");
        keyC.Should().Be("signin:203.0.113.8");
    }

    private static HttpRequest Request(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        return context.Request;
    }
}
