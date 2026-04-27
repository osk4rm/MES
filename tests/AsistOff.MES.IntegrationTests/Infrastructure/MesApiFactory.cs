using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace AsistOff.MES.IntegrationTests.Infrastructure;

/// <summary>
/// Test web host that boots the full Gateway pipeline in-process, backed by a real
/// PostgreSQL instance provisioned via Testcontainers. Migrations and dev-tenant
/// seeding run on startup, giving every test a known authenticated user
/// (<c>admin@dev.local</c> / <c>Passw0rd!</c>).
/// </summary>
public sealed class MesApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TenantName = "dev";
    public const string AdminEmail = "admin@dev.local";
    public const string AdminPassword = "Passw0rd!";

    private const string JwtSigningKey =
        "integration-tests-signing-key-must-be-at-least-32-bytes-long-1234567890";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("mes_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private DbConnection? _connection;
    private Respawner? _respawner;

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // The Gateway uses WebApplication.CreateBuilder which prevents post-build
        // additions to the ConfigurationManager (HostApplicationBuilder.ApplyChanges
        // throws ObjectDisposedException when ConfigureAppConfiguration is used).
        // Inject our test configuration via environment variables instead — they
        // are picked up by the default chained configuration during host build.
        ApplyTestEnvironment();

        // Boot the host once to apply migrations & seed the dev tenant.
        _ = Server;

        _connection = new NpgsqlConnection(ConnectionString);
        await _connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            // Preserve the dev tenant + its admin user so all authenticated
            // tests can sign in without re-running the full seeder.
            SchemasToInclude = ["config", "production", "files"],
        });
    }

    private void ApplyTestEnvironment()
    {
        var attachmentsRoot = Path.Combine(Path.GetTempPath(), "mes-it-" + Guid.NewGuid().ToString("N"));

        var settings = new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = Environments.Development,
            ["postgres__connectionString"] = ConnectionString,
            ["ConnectionStrings__DefaultConnection"] = ConnectionString,
            ["auth__IssuerSigningKey"] = JwtSigningKey,
            ["auth__Issuer"] = "AsistOff.MES",
            ["auth__ValidIssuer"] = "AsistOff.MES",
            ["auth__Expiry"] = "01:00:00",
            ["auth__ValidateAudience"] = "false",
            ["auth__RequireAudience"] = "false",
            ["auth__ValidateIssuer"] = "true",
            ["auth__ValidateLifetime"] = "true",
            ["auth__ValidateIssuerSigningKey"] = "true",
            ["Seed__Enabled"] = "true",
            ["Seed__Tenants__0__Name"] = TenantName,
            ["Seed__Tenants__0__DisplayName"] = "Dev Tenant",
            ["Seed__Tenants__0__ContactEmail"] = AdminEmail,
            ["Seed__Tenants__0__AdminPassword"] = AdminPassword,
            ["cors__allowedOrigins__0"] = "http://localhost",
            ["Attachments__LocalStorage__RootPath"] = attachmentsRoot,
            // Override the Seq sink (declared in appsettings.json) with another
            // Console sink to avoid slow, noisy connect attempts to a non-existent
            // collector during the test run.
            ["Serilog__WriteTo__1__Name"] = "Console",
            // Quieten EF Core command logging in tests — appsettings.json has it
            // at Information; bump Microsoft.* + EF down to Warning.
            ["Serilog__MinimumLevel__Default"] = "Warning",
            ["Serilog__MinimumLevel__Override__Microsoft"] = "Warning",
            ["Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore.Database.Command"] = "Warning",
            ["Serilog__MinimumLevel__Override__Microsoft.AspNetCore"] = "Warning",
            ["Logging__LogLevel__Default"] = "Warning",
            ["Logging__LogLevel__Microsoft.AspNetCore"] = "Warning",
        };

        foreach (var (key, value) in settings)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public async Task ResetDatabaseAsync()
    {
        if (_respawner is null || _connection is null)
        {
            return;
        }

        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync();
        }

        await _respawner.ResetAsync(_connection);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        return base.CreateHost(builder);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // xUnit's IAsyncLifetime.DisposeAsync returns Task while WebApplicationFactory
    // exposes ValueTask. Forward explicitly to satisfy both interfaces.
    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();
}
