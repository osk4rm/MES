# MES

AsistOff MES is a modular-monolith Manufacturing Execution System with a .NET 9 / ASP.NET Core
backend and a Vue 3 SPA frontend.

## Quick start (Docker Compose)

```bash
docker compose up --build
```

On boot the Gateway:

1. Applies all pending EF Core migrations for every registered `DbContext` (see
   `AsistOff.MES.Shared.Infrastructure.MigrationExtensions.ApplyAllPendingMigrations`).
2. Runs every registered `ISeeder`. When the environment is `Development` and
   `Seed:Enabled` is `true`, `DevTenantSeeder` provisions the tenants configured under
   `Seed:Tenants` in `appsettings.Development.json`. The seeder is **idempotent** — it skips
   any tenant whose `Name` already exists — and uses the exact same `CreateTenantCommand`
   pipeline as public registration, so the tenant-admin user is also created through the
   regular `TenantCreatedEvent` listener.

Default development credentials (from `AsistOff.MES.Gateway/appsettings.Development.json`):

| Field     | Value              |
|-----------|--------------------|
| Tenant    | `dev`              |
| Email     | `admin@dev.local`  |
| Password  | `Passw0rd!`        |

Sign in with:

```bash
curl -X POST http://localhost:5080/api/auth/sign-in \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@dev.local","password":"Passw0rd!"}'
```

Then use the returned `AccessToken` as a bearer token, or log in through the Vue SPA.

### Adding more dev tenants

Append entries to the `Seed:Tenants` array in `appsettings.Development.json`, or override via
environment variables, e.g.:

```bash
Seed__Tenants__1__Name=qa
Seed__Tenants__1__DisplayName=QA Tenant
Seed__Tenants__1__ContactEmail=admin@qa.local
Seed__Tenants__1__AdminPassword=Passw0rd!
```

### Self-service tenant registration (public)

The same flow is available for real registrations at `POST /api/tenants` (anonymous). See
`AsistOff.MES.Multitenancy/Controllers/TenantsController.cs`.

### Disabling the seeder

Set `Seed:Enabled=false` (or run the Gateway outside `Development`). `DevTenantSeeder`
short-circuits when either condition is false, so production images never auto-provision
tenants.

## Useful commands

| Command | Description |
|---------|-------------|
| `dotnet run --project AsistOff.MES.Gateway` | Start the backend API (port 5080) |
| `cd AsistOff.MES.Web && npm run dev` | Start the frontend dev server |
| `cd AsistOff.MES.Web && npm run build` | Production build (`vue-tsc` + Vite) |

### EF Core migrations

```bash
dotnet ef migrations add <Name> \
  --project AsistOff.MES.Shared.Infrastructure \
  --startup-project AsistOff.MES.Gateway \
  --context DefaultContext
```
