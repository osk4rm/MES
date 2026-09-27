# Frontend architecture

How the Vue 3 single-page application is built, served, and kept in session.
The HTTP surface it consumes is defined in [Endpoint catalog](endpoints.md);
the deployment that serves it is defined in [Deployment](deployment.md). Auth
claims, permissions, and the tenant model it relies on are defined in
[Authentication and RBAC](auth-rbac.md) and [Multitenancy](multitenancy.md).

## Stack and project shape

`AsistOff.MES.Web` is a Vue 3 plus Vite plus Pinia SPA (Vue `^3.5.17`, Vite
`^8.1.5`, Pinia `^4.0.2`, Axios `^1.19.0`, vue-i18n `^11.4.6`, vue-router
`^5.2.0`, PrimeIcons for iconography; production build is `vue-tsc -b` plus
`vite build`). Entry (`src/main.ts`) creates the app, installs Pinia, the
router, and i18n, loads the global stylesheet plus PrimeIcons, and mounts on
`#app` — with no persisted auth load: login state lives only in memory and
the guard re-proves it through the refresh cookie on every protected
navigation. `src/App.vue` renders `<AppErrorBoundary><router-view
/></AppErrorBoundary>` plus `<AppToastHost/>`; there is no global
`app.config.errorHandler` — the boundary component is the handler.

Business calls live in one typed service module per area
(`src/services/*Service.ts`: products, orders, confirmations, genealogy,
OEE, telemetry, schedule, roles, tenants, and the rest); each is a thin
async wrapper over the shared axios instance returning typed promises.

## In-house `App*` components

There is no third-party component library. `src/components/ui/` holds 25
`App*.vue` primitives used by every view — `AppTable`, `AppPagination`,
`AppFilterBar`, `AppFormField`, `AppInput`, `AppNumberInput`, `AppSelect`,
`AppTextarea`, `AppAutocomplete`, `AppCheckbox`, `AppButton`, `AppModal`,
`AppConfirmDialog`, `AppCard`, `AppPageHeader`, `AppBreadcrumbs`,
`AppBadge`, `AppSpinner`, `AppLoadingState`, `AppDataState`,
`AppErrorState`, `AppEmptyState`, `AppErrorBoundary`, `AppRowActions`, and
`AppToastHost` — plus the shell layout (`AppShell`, `AppSideNav`,
`AppTopBar`) and three domain widgets (`SpcControlChart`,
`RecipeVersionEditor`, `AttachmentsPanel`).

## Routing and route guards

`src/router.ts` uses `createWebHistory` with lazy-loaded views. `/`
redirects to `/dashboard`; `/login` and `/register` are the only public
routes (`meta.public`). Everything else renders inside `AppShell`:
`dashboard`, `production/*` (orders, order detail, scrap, andon, recipes,
recipe detail, spc-characteristics, downtime, lots, operator-panel,
telemetry, opcua-connections, kanban), `schedule` and
`schedule/dispatch`, `reports/oee`, `reports/reliability` and
`reports/telemetry` (every dashboard lives under Reports; the old
`production/telemetry-dashboard` path redirects to `reports/telemetry`), `settings`
(still a coming-soon view) and `settings/roles`, and `configuration/*`
(products, product-groups, measure-units, warehouses, departments, machines,
operators, skills, shifts, reason-codes, operation-templates, maintenance,
maintenance-plans). Unknown paths render `NotFoundView` inside the shell.

The `beforeEach` guard aborts pending list fetches, then enforces session
before render: an unauthenticated navigation to a protected route first tries
`restoreSession()` (a refresh-cookie round-trip, so a page reload keeps the
user signed in without stored tokens) and otherwise redirects to
`/login?redirect=<fullPath>`; a signed-in user visiting login or register is
sent to the dashboard. The deepest route carrying `meta.permission` wins and
is matched exactly against the in-memory permission list — on mismatch the
user lands on the dashboard with an access-denied toast. Only
`settings/roles` carries a permission (`tenant.admin`); every other list is
intentionally readable by any signed-in user because the backend remains the
permission enforcer. `afterEach` sets `document.title` through the
navigation map and the current locale.

## Session over axios: cookies, refresh, redirect

`src/services/http.ts` builds a single axios instance with the resolved API
base URL, a 15 s default timeout (overridable via `VITE_API_TIMEOUT_MS`), and
`withCredentials: true`. Sessions travel as the httpOnly `mes_access` /
`mes_refresh` cookies set by the backend; the client never injects a bearer
header and never persists tokens (the only `locale` exception is the i18n
preference, not a credential).

- **Correlation:** every request carries `X-Correlation-ID`, minted when
  absent and preserved across the retry below, so a server-side trace joins
  to the exact client action (see [Observability](observability.md)).
- **Refresh and redirect (`authService.ts`):** sign-in posts credentials and
  reads permissions from the body's claims (fail-closed to empty);
  `restoreSession` posts to the refresh endpoint and returns the session
  (`ok`, permissions, e-mail). Any `401` outside the auth endpoints clears
  auth state and navigates to login with the return path (suppressed on `/`,
  `/login`, and `/register` to avoid redirect loops).
- **Resilience:** exactly one retry, for idempotent GETs only, on network
  failure, offline, timeout, or 502/503/504 after 250 ms with the same
  correlation ID; mutations, aborts, and other statuses never retry.
  Navigating away (or unmounting) aborts tracked list fetches via the shared
  request controller, and every data view wires its error row to a retry
  action.

## Error boundary and feedback

`AppErrorBoundary` catches render crashes via `onErrorCaptured`, shows a
fallback with a fresh correlation ID plus the message and a reload action,
and resets on route change while the shell stays mounted.
`AppErrorState`/`AppDataState` rows surface fetch failures with retry;
`extractErrorMessage` prefers the backend `detail`/`title` before falling
back to transport text. Authorization and conflict outcomes surface as toasts
(403 access-denied, 409 concurrency), and the boundary display includes the
correlation ID that also lands in the backend `traceId` envelope. There is no
service worker or offline queue and no frontend log shipper: an offline
client sees the timeout message and retries explicitly.

## Internationalization

`src/i18n.ts` bundles two locales with full key parity: Polish (default —
`localStorage.locale` when set to `pl` or `en`, otherwise `pl`) and English
as fallback (`fallbackLocale: 'en'`, missing-key warnings off). Locales ship
in a single file with no lazy-loaded chunks; every user-visible string,
including route titles, toast copy, and error text, resolves through it.
