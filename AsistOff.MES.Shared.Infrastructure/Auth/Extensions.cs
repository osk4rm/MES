using System.Text;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Modules;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace AsistOff.MES.Shared.Infrastructure.Auth;

public static class Extensions
{
    public static IServiceCollection AddAuth(this IServiceCollection services, IHostEnvironment? hostEnvironment = null,
        IList<IModule>? modules = null, Action<JwtBearerOptions>? optionsFactory = null)
    {
        var options = services.GetOptions<AuthOptions>("auth");
        AuthOptionsValidator.Validate(options, hostEnvironment?.IsProduction() == true);
        services.AddSingleton<IAuthManager, AuthManager>();

        if (options.AuthenticationDisabled)
        {
            // Bypass semantics (issue #332, security audit 2026-09-26, Medium):
            // auth:AuthenticationDisabled is an explicit local-development
            // escape hatch only. When active, DisabledAuthenticationPolicyEvaluator
            // unconditionally succeeds every authorization check, so all
            // RequirePermission-guarded writes pass with no credential and no
            // tenant attribution, contradicting the default-deny posture.
            // Fail closed everywhere except Development: Staging/Test/Production
            // (or an unknown host environment) refuse to boot instead of serving
            // unauthenticated traffic. Development boots and emits an
            // unmistakable warning via AuthenticationDisabledWarningService.
            if (hostEnvironment?.IsDevelopment() != true)
            {
                var environmentName = hostEnvironment?.EnvironmentName ?? "<unknown>";
                throw new InvalidOperationException(
                    $"auth:AuthenticationDisabled=true is only allowed in the Development environment " +
                    $"(current: '{environmentName}'). Refusing to start with authentication disabled outside " +
                    "local development. Unset auth:AuthenticationDisabled to enforce JWT + RBAC.");
            }

            services.AddSingleton<IPolicyEvaluator, DisabledAuthenticationPolicyEvaluator>();
            services.AddHostedService<AuthenticationDisabledWarningService>();
        }

        var tokenValidationParameters = new TokenValidationParameters
        {
            RequireAudience = options.RequireAudience,
            // Fall back to the single Issuer/Audience shorthands when the
            // Valid* variants are unset, so `auth:Audience` alone is enough
            // to validate tokens issued with that audience (and likewise for
            // the issuer). Production still requires one of them to be set —
            // see AuthOptionsValidator.
            ValidIssuer = options.ValidIssuer ?? options.Issuer,
            ValidIssuers = options.ValidIssuers,
            ValidateActor = options.ValidateActor,
            ValidAudience = options.ValidAudience ?? options.Audience,
            ValidAudiences = options.ValidAudiences,
            ValidateAudience = options.ValidateAudience,
            ValidateIssuer = options.ValidateIssuer,
            ValidateLifetime = options.ValidateLifetime,
            ValidateTokenReplay = options.ValidateTokenReplay,
            ValidateIssuerSigningKey = options.ValidateIssuerSigningKey,
            SaveSigninToken = options.SaveSigninToken,
            RequireExpirationTime = options.RequireExpirationTime,
            RequireSignedTokens = options.RequireSignedTokens,
            ClockSkew = TimeSpan.Zero
        };

        if (string.IsNullOrWhiteSpace(options.IssuerSigningKey))
        {
            throw new ArgumentException("Missing issuer signing key.", nameof(options.IssuerSigningKey));
        }

        if (!string.IsNullOrWhiteSpace(options.AuthenticationType))
        {
            tokenValidationParameters.AuthenticationType = options.AuthenticationType;
        }

        var rawKey = Encoding.UTF8.GetBytes(options.IssuerSigningKey);
        tokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(rawKey);

        if (!string.IsNullOrWhiteSpace(options.NameClaimType))
        {
            tokenValidationParameters.NameClaimType = options.NameClaimType;
        }

        if (!string.IsNullOrWhiteSpace(options.RoleClaimType))
        {
            tokenValidationParameters.RoleClaimType = options.RoleClaimType;
        }

        services
            .AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(o =>
            {
                o.Authority = options.Authority;
                o.Audience = options.Audience;
                if (!string.IsNullOrWhiteSpace(options.MetadataAddress))
                {
                    o.MetadataAddress = options.MetadataAddress;
                }
                o.SaveToken = options.SaveToken;
                o.RefreshOnIssuerKeyNotFound = options.RefreshOnIssuerKeyNotFound;
                o.RequireHttpsMetadata = options.RequireHttpsMetadata;
                o.IncludeErrorDetails = options.IncludeErrorDetails;
                o.TokenValidationParameters = tokenValidationParameters;
                if (!string.IsNullOrWhiteSpace(options.Challenge))
                {
                    o.Challenge = options.Challenge;
                }

                o.Events = new JwtBearerEvents
                {
                    // Cookie transition (issue #241): accept the httpOnly
                    // access cookie when no Authorization header is present,
                    // so cookie-only callers authenticate. Header callers
                    // keep working unchanged — the header wins when both are
                    // present.
                    OnMessageReceived = context =>
                    {
                        var authorization = context.Request.Headers.Authorization.ToString();
                        if (string.IsNullOrWhiteSpace(authorization)
                            && context.Request.Cookies.TryGetValue(AuthCookies.AccessCookieName, out var cookieToken)
                            && !string.IsNullOrWhiteSpace(cookieToken))
                        {
                            context.Token = cookieToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                        if (logger.IsEnabled(LogLevel.Debug))
                        {
                            logger.LogDebug("JWT token validated for subject: {Subject}",
                                context.Principal?.FindFirst("sub")?.Value);
                        }
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogWarning("JWT authentication failed: {ExceptionType}", context.Exception.GetType().Name);
                        return Task.CompletedTask;
                    }
                };

                optionsFactory?.Invoke(o);
            });

        services.AddSingleton(options);
        services.AddSingleton(tokenValidationParameters);

        // Module permission policies (issue #329): every IModule.Policies entry is
        // registered as an MVC authorization policy requiring the matching
        // "permissions" claim value. Deduplicated by name (last-wins is
        // equivalent here since every registration uses the same
        // RequireClaim shape) because AuthorizationOptions.AddPolicy throws on
        // duplicate names and two modules may declare the same policy.
        // Null modules, null Policies collections and blank names register
        // nothing and throw nothing, preserving the default-deny pipeline.
        var policies = (modules ?? [])
            .SelectMany(x => x.Policies ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        services.AddAuthorization(authorization =>
        {
            foreach (var policy in policies)
            {
                authorization.AddPolicy(policy, x => x.RequireClaim("permissions", policy));
            }

            // Global fallback authorization policy (issue #351, security audit
            // 2026-09-26, Low): endpoints without explicit auth metadata fail
            // closed with 401. Every API controller inherits [Authorize] from
            // ApiController, but any controller that does not derive from it
            // (today: ErrorsController at /error) would otherwise be
            // anonymously reachable by default. The HTTP edge must mirror the
            // application-layer default-deny (AuthorizationBehavior): only
            // endpoints carrying [AllowAnonymous] (sign-in/refresh, tenant
            // self-registration + lookup, health probes, /error) stay
            // reachable without authentication.
            authorization.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}
