using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Integration.Tests.Infrastructure;

/// <summary>
/// Test-only middleware (issue #323) letting endpoint tests control the TCP
/// source address deterministically: when a request carries
/// <c>X-Test-Remote-Ip</c> with a valid IP, <c>Connection.RemoteIpAddress</c>
/// is set to it before any production middleware runs (notably before
/// <c>UseForwardedHeaders</c> and <c>UseRateLimiter</c>), then the header is
/// removed so production logic never sees it. Registered unconditionally in
/// <see cref="MesWebApplicationFactory"/>; production never registers it, so
/// the header is inert outside tests. Malformed values are ignored safely.
/// </summary>
public sealed class TestRemoteIpStartupFilter : IStartupFilter
{
    public const string HeaderName = "X-Test-Remote-Ip";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(HeaderName, out var values))
                {
                    var candidate = values.ToString().Split(',').Select(part => part.Trim()).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(candidate) && IPAddress.TryParse(candidate, out var ip))
                    {
                        context.Connection.RemoteIpAddress = ip;
                    }

                    context.Request.Headers.Remove(HeaderName);
                }

                await nextMiddleware();
            });

            next(app);
        };
    }
}
