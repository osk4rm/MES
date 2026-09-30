# Deployment — Linux server + Coolify (same model as asiki)

Target environment:

```
GitHub -> GitHub Actions (CI) -> Coolify (build from repo) -> Linux server -> HTTPS (Let's Encrypt)
```

Coolify builds images directly from `docker-compose.yml` in the repository, so no
external registry or complex pipeline is needed. The server compose file contains
**only MES services** (`postgres`, `seq`, `api`, `web` + the `migrate`/`production`/
`backup` profiles) — the agent `swarm` lives in the separate local-only
`docker-compose.swarm.yml` and is never deployed.

Only the `web` (frontend) container gets a public domain. Browsers stay
same-origin: the web nginx proxies `/api/*`, `/health` and `/swagger` to the
`api` container over the compose network — no CORS issues, and the `Secure`
`SameSite=Lax` auth cookies just work.

---

## 1. Server and Coolify

This guide assumes the same server that already runs asiki: Coolify is installed
and validated (`Servers → localhost → Validate`). If you are starting from
scratch, follow asiki's `docs/deployment.md` sections 1–2 (OVH VPS, Ubuntu 24.04,
Coolify install script, admin account).

Minimum for MES alone: 2 vCPU / 4 GB RAM (api 2G + postgres 1G + seq 1G limits).
Seq (structured-log UI) is optional — see section 8.

---

## 2. Domain and DNS

Two options, same as asiki.

### A. Without your own domain (quick test, recommended first)

Coolify can generate a working domain from the server IP via `sslip.io` and issue
a Let's Encrypt certificate for it. In the resource's **Domains** tab click
**Add domain → Generate domain** (service `web`), which yields:

```
https://<random>.TWOJ_IP.sslip.io   (e.g. https://2aznub....145.239.86.216.sslip.io)
```

Use the generated subdomain, **not** the bare `TWOJ_IP.sslip.io` — Coolify
creates no Traefik route for the bare form (requests 404).

> Set the domain **Port explicitly to `8080`** (confirm “Use this port anyway”
> when Coolify warns the port is unrecognized). The `web` image inherits
> `EXPOSE 80` from `nginx:alpine` alongside our `EXPOSE 8080`; with “Port
> missing” Traefik picks port 80 and every request ends in `502 Bad Gateway`.
> This bit us on the first deploy — explicit `8080` is required.

### B. Own domain (recommended long-term)

1. In the DNS zone add an **A** record: `mes.twojadomena.pl` → `TWOJ_IP`.
2. In Coolify set the `web` service domain to `mes.twojadomena.pl` (port `8080`).
3. Coolify issues the Let's Encrypt certificate once DNS points at the server.

---

## 3. Project in Coolify

1. **Projects → + Add** → name e.g. `mes` → environment `production`.
2. **+ New Resource → Docker Compose** (Git repository option).
3. Connect GitHub (GitHub App or deploy key / token).
4. Pick the repository, branch **`master`**, compose path: `/docker-compose.yml`.
   Coolify runs compose with an explicit `-f` and **ignores**
   `docker-compose.override.yml` (local-dev ports) — nothing extra is published
   on the server.
5. In **Environment Variables** add (generate secrets with
   `sh scripts/generate-env.sh` on any machine with sh/openssl):

    ```env
    ASPNETCORE_ENVIRONMENT=Production
    POSTGRES_DB=mes
    POSTGRES_USER=admin
    POSTGRES_PASSWORD=<openssl rand -base64 32>
    AUTH_ISSUER_SIGNING_KEY=<openssl rand -base64 48>
    SEQ_ADMIN_PASSWORD=<openssl rand -base64 24>
    API_BASE_URL=https://TWOJA_DOMENA
    VITE_API_BASE_URL=https://TWOJA_DOMENA
    CORS_ALLOWED_ORIGIN=https://TWOJA_DOMENA
    ```

   Replace `https://TWOJA_DOMENA` with e.g. `https://1-2-3-4.sslip.io`
   (no trailing slash). `API_BASE_URL` **must be the public frontend origin
   itself** — the web container renders it into `/config.js`, and because the
   origin matches, all `/api/*` calls stay same-origin through the nginx proxy.

   | Variable | Meaning |
   | --- | --- |
   | `ASPNETCORE_ENVIRONMENT` | `Production` on the server (strict CORS, no Swagger, secrets enforced) |
    | `POSTGRES_PASSWORD` | min. 32 bytes; enforced at boot, no defaults |
    | `AUTH_ISSUER_SIGNING_KEY` | min. 32 bytes (256 bits); signs the session JWTs |
    | `SEQ_ADMIN_PASSWORD` | required: first-run admin password for the Seq log UI (compose fails fast without it); the ingestion key (`SEQ_INGESTION_API_KEY`) is provisioned in the Seq UI after boot — see section 8 |
   | `API_BASE_URL` / `VITE_API_BASE_URL` | public frontend origin (runtime value wins, no rebuild needed) |
    | `CORS_ALLOWED_ORIGIN` | same public origin; Production refuses to boot without it |
    | `AllowedHosts` | public API host name(s), semicolon-separated (e.g. `mes.twojadomena.pl`); set as the `AllowedHosts` environment variable in Coolify. The shipped default allows only loopback hosts (`localhost;127.0.0.1;[::1]`, fail-closed) and any wildcard outside Development fails fast at startup, so set this on the server or every request gets `400` |
    | `TRUSTED_PROXY_NETWORK` | compose-network CIDR (e.g. `172.18.0.0/16`) so the API honors `X-Forwarded-Proto`/`X-Forwarded-For` from the web nginx; required for HSTS (`Strict-Transport-Security`) to reach browsers behind the TLS-terminating proxy. Empty (default) is default-deny: no HSTS is emitted and throttling uses the raw TCP source |
    | `BOOT_APPLY_MIGRATIONS` | default `true`: api applies EF Core migrations on boot (single-server path) |
| `BOOT_RUN_SEEDERS` | default `false`; seeders never run in Production via this path |
| `ATTACHMENTS_ROOT_PATH` | absolute blob root inside api/api-prod (default `/app/App_Data/attachments`), backed by the durable `attachments_data` volume (static mount — keep the default on the server, see “Attachment blob durability” below) |

6. In **Domains** (or the FQDN field of the `web` service) set the domain with
   the internal port **`8080`** (explicit — see 2A why “Port missing” breaks):
   - `https://mes.twojadomena.pl:8080`, or
   - the generated `https://<random>.TWOJ_IP.sslip.io:8080`.
7. Click **Deploy**.

Coolify builds and starts four containers:
- `postgres` (data in volume `postgres_data`),
- `seq` (logs in `seq_data`; no public domain needed),
- `api` (EF Core migrations apply automatically on boot; attachment blobs in
  volume `attachments_data`),
- `web` (nginx: SPA + proxy `/api`, `/health`, `/swagger` → `api:8080`).

### Attachment blob durability

Attachment binaries (certificates, photos, control-chart exports backing
Genealogy evidence and the audit trail) live on disk under
`Attachments:LocalStorage:RootPath`, while their metadata rows (with
`StorageKey`) live in Postgres. A relative `RootPath` (the default
`App_Data/attachments`) resolves against the host content root (`/app` in the
image), never against the process working directory; an absolute path passes
through unchanged.

In compose both `api` and `api-prod` mount the named volume
`attachments_data` at the static path `/app/App_Data/attachments` and export
`ATTACHMENTS_ROOT_PATH` (default `/app/App_Data/attachments`) as
`Attachments__LocalStorage__RootPath`, so the configured root and the mount
stay in sync and blobs survive `docker compose down` (volumes are kept),
container recreates, and redeploys. The mount target must stay a static path:
Coolify rejects variable interpolation (`${...}`) in volume targets and fails
the deployment, so a custom `ATTACHMENTS_ROOT_PATH` changes only the app
config, not the mount — keep the default on the server. The image seeds
`/app/App_Data/attachments` owned by the non-root `app` user, so a fresh
volume is writable without running as root. The resolved absolute blob root
is logged at startup (`Attachment blob storage root: ...`) — check it after
deploy to verify the mount.

Durability proof after a deploy or a compose change:

```bash
# upload a file, remember its download URL (GET /api/attachments/{id}/download)
curl -o before.bin <download-url> && sha256sum before.bin
docker compose down && docker compose up -d
curl -o after.bin <download-url> && sha256sum after.bin
# the two hashes must match; `docker volume ls` still shows attachments_data
```

### Updates

- Manually: **Deploy** in Coolify.
- Automatically: webhook from GitHub Actions (section 6) or **Auto Deploy** for
  the `master` branch in Coolify.

---

## 4. After deploy — smoke test

1. Open `https://TWOJA_DOMENA` — the login page loads (served by `web`).
2. Register a tenant: **Register** → tenant name + admin account
   (`POST /api/tenants`, anonymous, per-IP rate-limited).
3. Log in as the tenant admin.
4. Create one product (`Configuration → Products`) and one Production Order.
5. Restart the stack in Coolify (**Restart**) and verify the data is still there.

If the UI loads but login fails with a network error, check in the running `web`
container what backend URL was rendered:

```bash
docker exec <project>-web-1 cat /usr/share/nginx/html/config.js
# window.__MES_CONFIG__ = { apiBaseUrl: "https://TWOJA_DOMENA" };
```

It must equal the public origin. If not, fix `API_BASE_URL` in Coolify and
**Redeploy** (or restart `web` — no rebuild needed).

---

## 5. Backup and restore PostgreSQL

Scripts in the repository (same convention as asiki):

```bash
cd /opt/mes              # directory Coolify cloned the repo into (or your copy)
sh scripts/backup-db.sh  # writes /opt/mes-backups/mes_YYYY-MM-DD_HHMMSS.sql.gz
sh scripts/restore-db.sh /opt/mes-backups/mes_2026-01-01_030000.sql.gz
```

Automatic backup (cron, daily 3:00):

```bash
crontab -e
# add:
0 3 * * * cd /opt/mes && BACKUP_DIR=/opt/mes-backups sh scripts/backup-db.sh >> /var/log/mes-backup.log 2>&1
```

The script deletes copies older than 14 days (`RETENTION_DAYS`).
For restores, first stop everything writing to the database
(`docker compose stop api web`), restore, then start again and confirm
`GET /health/ready` returns `200`.

The compose `backup` service (`production`/`backup` profiles, nightly `pg_dump`
into the `pg_backups` volume) is an alternative for strict plant setups — see
`docs/production-runbook.md`.

---

## 6. GitHub Actions → deployment

Workflow `.github/workflows/ci.yml`:

- on every PR: backend build + unit/integration tests, frontend type-check + build,
- on `master`: the same, then a Coolify deploy-webhook call (if configured).

In GitHub → repository **Settings → Secrets and variables → Actions** add:

| Secret | Where from |
| --- | --- |
| `COOLIFY_WEBHOOK` | Coolify → project → **Webhooks → Deploy Webhook** (URL) |
| `COOLIFY_TOKEN` | Coolify → **Keys & Tokens → API tokens** (Bearer) |

If you don't want CI-triggered deploys, skip the secrets — the workflow skips
the deploy step.

> Think twice before enabling this on MES: unlike asiki, `master` here receives
> several automated swarm merges per day, and every triggered deploy restarts
> the API (~1–2 min downtime). Alternatives: deploy manually with the Coolify
> **Deploy** button, or turn on the resource's **Auto Deploy** polling in
> Coolify (no secrets needed). The API must be enabled in Coolify instance
> settings before API tokens can be issued.

---

## 7. Strict plant rollouts (optional)

The default path above auto-applies migrations on `api` boot
(`BOOT_APPLY_MIGRATIONS=true`). For zero-downtime plant databases use the
strict path instead: the one-shot `migrate` job upgrades the schema and must
complete before `api-prod` serves traffic (production boot itself never
migrates or seeds). Full procedure: `docs/production-runbook.md`.

```bash
docker compose --profile production up --build -d
```

---

## 8. Seq (optional, authenticated)

`seq` ships MES logs to a searchable UI. It runs by default in the compose
stack but needs no public domain — and since issue #377 it **requires
authentication**: anonymous requests to the Seq UI and anonymous ingestion
posts are rejected. Log events carry operator activity and tenant data, so
an open Seq is an open data leak.

Local dev binds the published port to **localhost only**
(`127.0.0.1:${SEQ_PORT:-5341}:80` in `docker-compose.override.yml`), so the
log browser stays convenient without exposing Seq to the LAN. Coolify
ignores the override file, so the server never publishes host ports at all.

| Variable | Meaning |
| --- | --- |
| `SEQ_PORT` | local host port for the Seq UI (default `5341`) |
| `SEQ_ADMIN_PASSWORD` | **required, no default**: first-run admin password for the Seq UI; compose fails fast without it (generate with `sh scripts/generate-env.sh`) |
| `SEQ_INGESTION_API_KEY` | ingestion API key the api/api-prod Serilog sink authenticates with; provisioned once in the Seq UI (see bootstrap below) |

First boot on any machine (fresh `seq_data` volume):

```bash
cp .env.example .env
sh scripts/generate-env.sh   # fills POSTGRES_PASSWORD + AUTH_ISSUER_SIGNING_KEY + SEQ_ADMIN_PASSWORD
docker compose up -d
```

Then wire log shipping (Seq only honors `SEQ_FIRSTRUN_*` when the volume is
initialized, so the ingestion key is a one-time manual step):

1. Open `http://localhost:5341`, log in as `admin` with `SEQ_ADMIN_PASSWORD`
   (Seq forces a password change on this first login — that is expected).
2. **Settings → API Keys → Add API Key** (Ingest permission), copy the key.
3. Paste it as `SEQ_INGESTION_API_KEY` in `.env`, then
   `docker compose restart api` — the api resumes shipping logs to Seq.
   Until the key is set the api still boots and logs to the console; Seq
   just rejects its anonymous posts (fail-closed).

Upgrading a stack that already ran Seq **without** authentication: the
`SEQ_FIRSTRUN_*` variables have no effect on the existing `seq_data`
volume. Either enable authentication inside the running Seq
(**Settings → Users**, set the admin password and require authentication
for HTTP ingestion), or recreate the volume and follow the first-boot flow
above (`docker compose down seq && docker volume rm <project>_seq_data`
loses stored logs — export first if they matter).
