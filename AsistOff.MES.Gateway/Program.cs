using AsistOff.MES.Gateway;
using AsistOff.MES.Multitenancy;
using AsistOff.MES.Shared.Abstractions.Seeder;
using AsistOff.MES.Shared.Infrastructure;
using AsistOff.MES.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureModules();
builder.Configuration.AddUserSecrets<Program>();

var modules = ModuleLoader.LoadModules();
var assemblies = ModuleLoader.LoadAssemblies();

builder.Services.AddMediatR(cfg =>
{
    foreach (var assembly in assemblies)
        cfg.RegisterServicesFromAssembly(assembly);
});

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
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders("Content-Disposition");
        }
    });
});

builder.Services
    .AddPresentation()
    .AddInfrastructure(builder.Configuration, assemblies);

builder.Services.AddMultitenancy(builder.Configuration);

foreach (var module in modules)
{
    module.Register(builder.Services, builder.Configuration);
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => {
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
app.MapControllers();

app.Run();
