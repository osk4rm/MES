# Local e2e / dev setup (single source of truth)

This is the **one place** both `scripts/e2e/app.ps1` and the `mes-e2e` skill
point to for the local database and stack prerequisites.

## Prerequisites

- .NET 10 SDK, Node 20+, PowerShell 7 (`pwsh`), Docker (for PostgreSQL).
- No manual env edits are needed for a stock checkout.

## Database (required)

The backend needs PostgreSQL on `localhost:5432` with the compose defaults:

| Thing | Value |
|---|---|
| Database | `mes` |
| User | `admin` |
| Password | `root` |
| Port | `5432` |

One-command setup:

```powershell
docker compose up -d postgres
```

The backend connection string resolves in this order (last wins):

1. `AsistOff.MES.Gateway/appsettings.Development.json`
   (`postgres:connectionString`, placeholder values only),
2. user secrets (`dotnet user-secrets`),
3. environment variable `postgres__connectionString` (e.g. exported by
   `docker compose` or CI),
4. command-line args.

Environment variables **override** user secrets by design (see
`AsistOff.MES.Gateway/Program.cs` — env sources are re-added after
`AddUserSecrets`). So `postgres__connectionString=...` always takes effect;
you never need to edit secrets to point at a different database.

Equivalent connection string for the defaults:

```
Host=localhost;Port=5432;Database=mes;Username=admin;Password=root
```

If the database is not reachable the backend exits at migration time and
`scripts/e2e/app.ps1 -Action start` reports the failure (the e2e agent
surfaces this as `VERDICT: E2E_BLOCKED`).

## Stack (ports)

| Thing | Value |
|---|---|
| Backend (`http` launch profile) | `http://localhost:5243` (`/health`, `/swagger`) |
| Frontend (vite, `strictPort`) | `http://localhost:5173` |
| Dev login (seeded) | `admin@dev.local` / `Passw0rd!` (tenant `dev`) |

Start / stop / status:

```powershell
pwsh -File scripts/e2e/app.ps1 -Action start
pwsh -File scripts/e2e/app.ps1 -Action status
pwsh -File scripts/e2e/app.ps1 -Action stop
```

`start` exports `VITE_API_BASE_URL=http://localhost:5243/` for the vite child
process, so the UI always calls the backend this run started (process env
wins over `AsistOff.MES.Web/.env.development`). Vite uses `strictPort: true`,
so a busy `:5173` fails fast with a clear error instead of silently moving
to `:5174`. CORS in `appsettings.Development.json` allows the vite fallback
origin `http://localhost:5174` as well, for manual `npm run dev` runs outside
the script.
