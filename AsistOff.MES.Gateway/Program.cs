using AsistOff.MES.Gateway;
using AsistOff.MES.Multitenancy;
using AsistOff.MES.Shared.Abstractions.Seeder;
using AsistOff.MES.Shared.Infrastructure;

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

// TEMP - TODO: przeniesc do konfiguracji 
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        b =>
        {
            b.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders("Content-Disposition");
        });
});

builder.Services
    .AddPresentation()
    .AddInfra(builder.Configuration, assemblies);

// Remove individual AddMediatR registrations from other projects to avoid duplicates
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

// TEMP
app.UseCors("AllowAll");

app.UseExceptionHandler("/error");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();