using AsistOff.MES.Shared.Infrastructure.Protection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Shared.Tests.Protection;

/// <summary>
/// Verifies the default security headers: always-on nosniff / CSP /
/// Referrer-Policy, HSTS only over TLS, and the Development Swagger UI
/// exemption for the restrictive content security policy.
/// </summary>
public class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task Invoke_HttpRequest_SetsRestrictiveHeaders_WithoutHsts()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Content-Type-Options"].Should().ContainSingle().Which.Should().Be("nosniff");
        context.Response.Headers["Content-Security-Policy"].Should().ContainSingle()
            .Which.Should().Be(SecurityHeadersMiddleware.ContentSecurityPolicyValue);
        context.Response.Headers["Referrer-Policy"].Should().ContainSingle()
            .Which.Should().Be(SecurityHeadersMiddleware.ReferrerPolicyValue);
        context.Response.Headers.Should().NotContainKey("Strict-Transport-Security");
    }

    [Fact]
    public async Task Invoke_HttpsRequest_EmitsStrictTransportSecurity()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["Strict-Transport-Security"].Should().ContainSingle()
            .Which.Should().Be(SecurityHeadersMiddleware.StrictTransportSecurityValue);
        context.Response.Headers["X-Content-Type-Options"].Should().ContainSingle().Which.Should().Be("nosniff");
    }

    [Fact]
    public async Task Invoke_SwaggerUi_SkipsContentSecurityPolicy_ButKeepsNosniff()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Path = "/swagger/index.html";
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().NotContainKey("Content-Security-Policy");
        context.Response.Headers["X-Content-Type-Options"].Should().ContainSingle().Which.Should().Be("nosniff");
    }

    [Fact]
    public async Task Invoke_ForwardedHttpsScheme_EmitsStrictTransportSecurity()
    {
        // Arrange - behind the TLS-terminating proxy UseForwardedHeaders
        // rewrites Scheme to https before this middleware runs (issue #369);
        // an already-rewritten https scheme must therefore carry HSTS.
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["Strict-Transport-Security"].Should().ContainSingle()
            .Which.Should().Be(SecurityHeadersMiddleware.StrictTransportSecurityValue);
    }

    [Fact]
    public async Task Invoke_UntrustedForwardedProtoWithoutSchemeRewrite_EmitsNoHsts()
    {
        // Arrange - fail-closed (issue #369): with no trusted proxy entry the
        // Forwarded Headers middleware ignores X-Forwarded-Proto, Scheme stays
        // http, and no HSTS is emitted. A spoofed header alone must never
        // enable HSTS.
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().NotContainKey("Strict-Transport-Security");
    }

    [Fact]
    public void StrictTransportSecurity_HasYearMaxAgeAndSubDomains()
    {
        // Arrange + Act
        var hsts = SecurityHeadersMiddleware.StrictTransportSecurityValue;

        // Assert
        hsts.Should().Contain("max-age=31536000");
        hsts.Should().Contain("includeSubDomains");
    }

    [Fact]
    public void ContentSecurityPolicy_IsRestrictive()
    {
        // Arrange + Act
        var csp = SecurityHeadersMiddleware.ContentSecurityPolicyValue;

        // Assert
        csp.Should().Contain("default-src 'self'");
        csp.Should().Contain("object-src 'none'");
        csp.Should().Contain("frame-ancestors 'none'");
        csp.Should().NotContain("unsafe-inline");
        csp.Should().NotContain("unsafe-eval");
    }
}
