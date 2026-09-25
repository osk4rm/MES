using System.Text;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Modules;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
            if (hostEnvironment?.IsProduction() == true)
            {
                throw new InvalidOperationException("Authentication cannot be disabled in a Production environment.");
            }

            services.AddSingleton<IPolicyEvaluator, DisabledAuthenticationPolicyEvaluator>();
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

        var policies = modules?.SelectMany(x => x.Policies) ?? [];
        services.AddAuthorization(authorization =>
        {
            foreach (var policy in policies)
            {
                authorization.AddPolicy(policy, x => x.RequireClaim("permissions", policy));
            }
        });

        return services;
    }
}
