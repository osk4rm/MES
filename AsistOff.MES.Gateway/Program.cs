using AsistOff.MES.Gateway;
using AsistOff.MES.Gateway.Protection;
using AsistOff.MES.Multitenancy;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Seeder;
using AsistOff.MES.Shared.Infrastructure;
using AsistOff.MES.Shared.Infrastructure.Boot;
using AsistOff.MES.Shared.Infrastructure.Correlation;
using AsistOff.MES.Shared.Infrastructure.Extensions;
using AsistOff.MES.Shared.Infrastructure.Health;
using AsistOff.MES.Shared.Infrastructure.Observability;
using AsistOff.MES.Shared.Infrastructure.Protection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

// Two-stage Serilog initialization. The bootstrap logger captures any failure
// during host build (DI errors, configuration parsing, etc.); once the host
// is built, UseSerilog re-reads configuration and replaces the bootstrap
// logger with the fully-configured pipeline (Console + Seq, enrichers, etc.).
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.ConfigureModules();
    builder.Configuration.AddUserSecrets<Program>();
    // Configuration precedence (last source wins): appsettings < user secrets
    // < environment variables < command line. WebApplication.CreateBuilder
    // already added env vars before this point, so re-adding them after
    // AddUserSecrets restores the intended order: `postgres__connectionString`
    // and other `__`-separated env overrides take effect over user secrets.
    // This is what lets `docker compose` / CI / e2e export a connection string
    // without editing local secrets. Command-line args stay highest.
    builder.Configuration.AddEnvironmentVariables();
    if (args.Length > 0)
    {
        builder.Configuration.AddCommandLine(args);
    }

    // Pull all logging configuration from appsettings (Serilog section). Sinks,
    // minimum levels, enrichers, and Seq URL are all declarative — no code
    // changes are required to add another sink or change verbosity per env.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .Enrich.WithThreadId()
        .Enrich.WithProperty("Application", "AsistOff.MES"));

    var modules = ModuleLoader.LoadModules();
    var assemblies = ModuleLoader.LoadAssemblies();

    builder.Services.AddMediatR(cfg =>
    {
        foreach (var assembly in assemblies)
            cfg.RegisterServicesFromAssembly(assembly);
    });

    builder.Services.AddMesHealthChecks();

    builder.Services.AddExceptionHandling();

    // OpenTelemetry traces/metrics (issue #252): OTLP export when an endpoint
    // is configured, Prometheus exposition when enabled, no-op otherwise.
    builder.Services.AddMesObservability(builder.Configuration);

    builder.Services.AddAbuseProtection(builder.Configuration);

    var allowedOrigins = builder.Configuration.GetSection("cors:allowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DefaultPolicy", policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                // AllowCredentials is required for the httpOnly auth-cookie
                // transport (issue #241): browsers only send cookies
                // cross-origin when the server opts in.
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .WithExposedHeaders("Content-Disposition", CorrelationIds.HeaderName);
            }
            else if (builder.Environment.IsDevelopment())
            {
                // Dev fallback without configured origins: mirror the above
                // but reflect any origin. SetIsOriginAllowed (instead of
                // AllowAnyOrigin) is required — ASP.NET Core refuses
                // AllowAnyOrigin combined with AllowCredentials.
                // NOTE: reflecting any origin together with AllowCredentials
                // is dev-only. Production requires explicit cors:allowedOrigins
                // (see the branch above); never enable this wildcard with
                // credentials outside Development.
                policy.SetIsOriginAllowed(_ => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .WithExposedHeaders("Content-Disposition", CorrelationIds.HeaderName);
            }
            else
            {
                throw new InvalidOperationException(
                    "cors:allowedOrigins must be configured with at least one origin in non-Development environments.");
            }
        });
    });

    builder.Services
        .AddPresentation()
        .AddInfrastructure(builder.Configuration, assemblies, builder.Environment);

    builder.Services.AddMultitenancy(builder.Configuration);

    foreach (var module in modules)
    {
        module.Register(builder.Services, builder.Configuration);
    }

    var app = builder.Build();

    // Correlation ID first: every request carries one opaque GUID from
    // browser to logs to error payload (issue #251). Reads inbound
    // X-Correlation-ID (generates a GUID when absent or invalid), echoes the
    // effective value on every response (including error responses from the
    // exception handler below), assigns HttpContext.TraceIdentifier, and
    // pushes the value into Serilog LogContext. Runs before tenant
    // resolution and never reads TenantId.
    app.UseMiddleware<CorrelationIdMiddleware>();

    // Stash the resolved tenant id for trace enrichment (issue #252): the
    // OTel response callback tags the server span after the request scope is
    // torn down, so the tenant must be captured while the scope is alive.
    // Runs before tenant resolution on the way in and captures on the way
    // out, after authentication has resolved the tenant; reads TenantId as
    // an opaque tag value only.
    app.UseMiddleware<TenantTraceContextMiddleware>();

    // Security headers first: every API response (including error responses
    // from the exception handler) carries nosniff / CSP / Referrer-Policy
    // and, over TLS, HSTS.
    app.UseMiddleware<SecurityHeadersMiddleware>();

    // Structured request logging: every HTTP request becomes a single log
    // record with method, path, status, elapsed ms, plus tenant/user/correlation
    // ids when available. Replaces the noisy default per-request lifecycle logs.
    app.UseSerilogRequestLogging(opts =>
    {
        opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? string.Empty);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
            diagnosticContext.Set("CorrelationId", CorrelationIds.GetCurrent(httpContext) ?? httpContext.TraceIdentifier);

            var tenantAccessor = httpContext.RequestServices.GetService<ICurrentTenantAccessor>();
            if (tenantAccessor is not null && tenantAccessor.TryGetTenantId(out var tenantId))
            {
                diagnosticContext.Set("TenantId", tenantId);
            }

            if (httpContext.User.Identity?.IsAuthenticated == true)
            {
                diagnosticContext.Set("UserName", httpContext.User.Identity.Name ?? string.Empty);
            }
        };
    });

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "AsistOff.MES.Gateway API V1");
            c.RoutePrefix = "swagger";
        });
    }

    // Add global exception handling middleware (MUST be early in pipeline)
    app.UseExceptionHandler();

    foreach (var module in modules)
    {
        module.Use(app);
    }

    // Production boot gate (issue #257): migrations and seeders only run
    // when the operator explicitly opts in via the Boot section
    // (Boot:ApplyMigrations / Boot:RunSeeders, or the Boot__* env vars).
    // Production defaults are both false, so a production boot never
    // auto-migrates the plant database and never runs seeders; Development
    // opts back in via appsettings.Development.json (true/true), preserving
    // the historical dev behavior. Fail fast on contradictory flags.
    // The branching lives in BootRunner (unit-tested without a database);
    // the callbacks below wire it to the real EF Core migrations and ISeeders.
    var bootOptions = builder.Configuration.GetSection(BootOptions.SectionName).Get<BootOptions>()
        ?? new BootOptions();

    // Migrate-job entrypoint for compose/production: `dotnet
    // AsistOff.MES.Gateway.dll --migrate-only` applies pending migrations
    // (and seeders when Boot:RunSeeders is set) and then exits without
    // serving traffic. The production compose profile runs this as a job
    // that must complete before the gateway container starts.
    var bootExecution = await BootRunner.RunAsync(
        bootOptions,
        args,
        migrateAsync: _ =>
        {
            using var migrateScope = app.Services.CreateScope();
            migrateScope.ServiceProvider.ApplyAllPendingMigrations(assemblies);
            return Task.CompletedTask;
        },
        seedAsync: async _ =>
        {
            using var seedScope = app.Services.CreateScope();
            var seeders = seedScope.ServiceProvider.GetServices(typeof(ISeeder));
            var seederCount = 0;
            foreach (var seeder in seeders)
            {
                await ((ISeeder)seeder!).Seed().ConfigureAwait(false);
                seederCount++;
            }

            return seederCount;
        });

    foreach (var bootMessage in bootExecution.Messages)
    {
        Log.Information("{BootMessage}", bootMessage);
    }

    if (!bootExecution.ServedTraffic)
    {
        return;
    }

    app.UseHttpsRedirection();
    app.UseCors("DefaultPolicy");
    // Per-IP fixed-window throttle for the anonymous bootstrap endpoints
    // (sign-in, tenant self-registration). Must precede authentication so
    // credential-stuffing bursts are rejected before any credential work.
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    // Prometheus scrape endpoint (issue #252): mapped only when
    // Observability:PrometheusEnabled is true; otherwise GET /metrics is 404.
    app.UseMesObservability();
    // Health probes for container orchestrators (issue #249). All three are
    // anonymous infrastructure endpoints with no tenant context: orchestrators
    // call them with no user or tenant. AllowAnonymous makes the opt-out
    // explicit and DisableRateLimiting guarantees sign-in burst traffic can
    // never throttle the probes with 429 (the global limiter only throttles
    // POST sign-in / tenant-signup, but the opt-out keeps probes immune to
    // future limiter changes too).
    // - GET /health/live runs only the dependency-free "self" check: 200 when
    //   the process serves traffic, even when PostgreSQL is unreachable.
    // - GET /health/ready runs only the PostgreSQL check: 200 when the
    //   database answers, 503 with problem details otherwise.
    // - GET /health is the historical endpoint, kept as a readiness alias.
    var liveOptions = new HealthCheckOptions
    {
        Predicate = HealthProbes.IsLiveCheck,
        ResponseWriter = HealthProbeResponseWriter.WriteResponseAsync
    };
    var readyOptions = new HealthCheckOptions
    {
        Predicate = HealthProbes.IsReadyCheck,
        ResponseWriter = HealthProbeResponseWriter.WriteResponseAsync
    };
    app.MapHealthChecks(HealthProbes.LivePath, liveOptions).AllowAnonymous().DisableRateLimiting();
    app.MapHealthChecks(HealthProbes.ReadyPath, readyOptions).AllowAnonymous().DisableRateLimiting();
    app.MapHealthChecks(HealthProbes.AliasPath, readyOptions).AllowAnonymous().DisableRateLimiting();
    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Exposed so integration tests can boot the real host via WebApplicationFactory<Program>.
public partial class Program;

