using AsistOff.MES.Gateway;
using AsistOff.MES.Multitenancy;
using AsistOff.MES.Shared.Abstractions.Seeder;
using AsistOff.MES.Shared.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureModules();

builder.Configuration.AddUserSecrets<Program>();

var modules = ModuleLoader.LoadModules();

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
    .AddInfra(builder.Configuration);

builder.Services.AddMultitenancy();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

foreach (var module in modules)
{
    module.Register(builder.Services);
    module.Use(app);
}

using var scope = app.Services.CreateScope();
var seeders = scope.ServiceProvider.GetServices(typeof(ISeeder));

foreach (var seeder in seeders)
{
    await ((ISeeder)seeder!).Seed();
}

// TEMP
app.UseCors("AllowAll");

app.UseExceptionHandler("/error");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();