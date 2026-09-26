using AsistOff.MES.Shared.Infrastructure.Protection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Shared.Tests.Protection;

/// <summary>
/// Verifies which requests the abuse-protection limiter throttles and how the
/// per-IP partition key is derived (issue #323): the partition key comes from
/// <c>Connection.RemoteIpAddress</c> only — spoofed <c>X-Forwarded-For</c>
/// headers never change it. Behind a trusted proxy the Forwarded Headers
/// middleware has already rewritten <c>RemoteIpAddress</c> to the real client
/// IP, so honoring <c>RemoteIpAddress</c> keeps the nginx deployment working.
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
    public void ResolveClientIp_IgnoresSpoofedForwardedHeader()
    {
        // Arrange - untrusted direct connection with a spoofed header: the
        // throttle must partition by the TCP source, not the header.
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.1.2.3");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.7, 198.51.100.2";

        // Act
        var ip = AbuseProtectionPolicy.ResolveClientIp(context);

        // Assert
        ip.Should().Be("10.1.2.3");
    }

    [Fact]
    public void ResolveClientIp_RotatingForwardedHeaders_AlwaysResolvesToSameRemoteIp()
    {
        // Arrange - one TCP source rotating the header to escape the limiter.
        var remote = System.Net.IPAddress.Parse("10.9.9.9");

        // Act
        var resolved = new[]
        {
            "203.0.113.1",
            "203.0.113.2",
            "198.51.100.99, 203.0.113.3",
            "not-an-ip",
            string.Empty,
            "   "
        }.Select(header =>
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = remote;
            context.Request.Headers["X-Forwarded-For"] = header;

            return AbuseProtectionPolicy.ResolveClientIp(context);
        }).ToList();

        // Assert
        resolved.Should().AllBe("10.9.9.9");
    }

    [Fact]
    public void ResolveClientIp_HonorsRemoteIp_RewrittenByTrustedProxyMiddleware()
    {
        // Arrange - behind a configured trusted proxy the Forwarded Headers
        // middleware has already rewritten RemoteIpAddress to the forwarded
        // client IP; the policy honors it via RemoteIpAddress (nginx
        // proxy_set_header X-Forwarded-For deployment keeps working).
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.7");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.7";

        // Act
        var ip = AbuseProtectionPolicy.ResolveClientIp(context);

        // Assert
        ip.Should().Be("203.0.113.7");
    }

    [Theory]
    [InlineData("not-an-ip,,,")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("203.0.113.7, not-an-ip, ")]
    public void ResolveClientIp_MalformedOrEmptyForwardedHeader_FallsBackToRemoteIp(string header)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.1.2.3");
        context.Request.Headers["X-Forwarded-For"] = header;

        // Act
        var ip = AbuseProtectionPolicy.ResolveClientIp(context);

        // Assert - never throws, never uses the header value.
        ip.Should().Be("10.1.2.3");
    }

    [Fact]
    public void ResolveClientIp_WithoutForwardedHeader_PartitionsByRemoteAddress()
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
        first.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.7");

        var second = new DefaultHttpContext();
        second.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.8");

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
