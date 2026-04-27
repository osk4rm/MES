using AsistOff.MES.Gateway;
using AsistOff.MES.Multitenancy;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Seeder;
using AsistOff.MES.Shared.Infrastructure;
using AsistOff.MES.Shared.Infrastructure.Extensions;
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

    builder.Services.AddHealthChecks();

    builder.Services.AddExceptionHandling();

    var allowedOrigins = builder.Configuration.GetSection("cors:allowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DefaultPolicy", policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .WithExposedHeaders("Content-Disposition");
            }
            else if (builder.Environment.IsDevelopment())
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .WithExposedHeaders("Content-Disposition");
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
            diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);

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

    using (var scope = app.Services.CreateScope())
    {
        scope.ServiceProvider.ApplyAllPendingMigrations(assemblies);
        var seeders = scope.ServiceProvider.GetServices(typeof(ISeeder));
        foreach (var seeder in seeders)
        {
            await ((ISeeder)seeder!).Seed();
        }
    }

    app.UseHttpsRedirection();
    app.UseCors("DefaultPolicy");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapHealthChecks("/health");
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

