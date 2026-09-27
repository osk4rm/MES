# AsistOff MES — Design System

The web UI is built from a small set of in-house components. There are **no runtime UI framework dependencies** beyond Vue, Pinia, Vue Router, Vue I18n, Axios and the `primeicons` font.

## Design tokens

All colors, spacing, radii, typography and control sizes are CSS custom properties defined in [`src/style.css`](./src/style.css). Always reference tokens via `var(--token)` rather than hard-coding hex values. A `data-theme="dark"` variant is opt-in.

Key token groups:

- `--color-primary` / `--color-primary-hover` / `--color-primary-soft` — stalowo-granatowy primary
- `--color-success` / `--color-warning` / `--color-danger` / `--color-info` / `--color-idle` (+ `*-soft`) — semantic statuses
- `--color-bg`, `--color-surface`, `--color-surface-sunken`, `--color-border`, `--color-divider`
- `--space-*`, `--radius-*`, `--control-height-*`, `--layout-*`
- `--font-family`, `--font-size-*`, `--font-weight-*`
- `--shadow-*`, `--transition-*`

## Component library (`src/components/ui/`)

All primitives are namespaced with the `App` prefix.

| Component          | Purpose                                                 |
|--------------------|---------------------------------------------------------|
| `AppButton`        | Primary / secondary / ghost / danger / subtle / link buttons; icons, loading. `link` is the text-only variant for navigation affordances (auth side panels) |
| `AppInput`         | Text / email / password input with prefix icon; `list` binds a `<datalist>` id, `autofocus` focuses on mount (scan-wedge fields), `enter` event fires on Enter |
| `AppDateTimeField` | Shared touch-friendly date-time entry (F-06): labelled `datetime-local` with 44 px targets, local-time hint + UTC note, app-locale `error` |
| `AppNumberInput`   | Numeric input with optional suffix                      |
| `AppTextarea`      | Multi-line text area                                    |
| `AppSelect`        | Native-backed select with label resolution              |
| `AppCheckbox`      | Boolean toggle with label                               |
| `AppFormField`     | Wraps a control with label, hint, required + error slot |
| `AppFilterBar`     | Container for list-page filters with a clear action     |
| `AppTable`         | Generic data table with sort-change events and slots    |
| `AppPagination`    | Paginator paired with `AppTable`                        |
| `AppRowActions`    | Icon-button cluster used inside table rows              |
| `AppModal`         | Centered modal with header/body/footer slots            |
| `AppConfirmDialog` | Pre-styled destructive / warning / info confirmation    |
| `AppToastHost`     | Toast stack; mount once in `App.vue`                    |
| `AppBadge`         | Status chip (semantic variants; optional `icon` + `dot` so signals never rely on color alone) |
| `AppCard`          | Content block with optional header/footer               |
| `AppPageHeader`    | Standard page header with breadcrumbs and actions slot  |
| `AppBreadcrumbs`   | Breadcrumb trail                                        |
| `AppEmptyState`    | Empty/Coming-soon placeholder                           |
| `AppSpinner`       | Minimal CSS spinner                                     |
| `AppLoadingState`  | Shared loading presentation (spinner + `common.loading` label, `role="status"`) |
| `AppErrorState`    | Shared error panel (`role="alert"`) with a working retry button (`common.retry` → `retry` event) |
| `AppDataState`     | List-region state machine: fixed `error > loading > empty > content` precedence with `retry` event |

Layout components live in `src/components/layout/`:
| Component    | Purpose                                      |
|--------------|----------------------------------------------|
| `AppShell`   | Grid layout wrapping sidenav + topbar + main |
| `AppSideNav` | Collapsible navigation from `sitemap.ts`     |
| `AppTopBar`  | User menu, locale switcher, env indicator    |

Shopfloor components live in `src/components/shopfloor/`:

| Component               | Purpose                                      |
|-------------------------|----------------------------------------------|
| `ShopfloorDensityToggle` | Shared comfortable/compact density toggle (F-14); 44 px touch minimum is the comfortable default, persisted per operator |

## Shopfloor form conventions (F-06 / F-19 / F-20)

- Timestamp entry uses `AppDateTimeField` (labelled, 44 px targets, local-time hint + UTC note); invalid input yields an app-locale message.
- Confirmation-family modals (Confirmation, scrap, downtime, Andon resolve) share the canonical field order: Work Center → quantity → reason → notes → timestamp. Per-flow i18n namespaces stay as aliases for the overlapping labels.
- Badge-on/scan fields accept scan-wedge Enter-terminated input (`enter` event) with `autofocus` (operator panel code field mirrors the lot scan field).

## Services
HTTP calls live in `src/services/*Service.ts`. They all share:

- `http` — the Axios instance with a JWT request interceptor and a `401 → /login` response interceptor.
- `extractErrorMessage(err, fallback)` — normalizes ASP.NET Core `ProblemDetails` / `ValidationProblemDetails` payloads to a toast-safe string.
- `buildPagedParams(req)` — strips empty values before sending paged requests.

## Stores (`src/stores/`)

- `authStore` — JWT + user in localStorage (`setAuth`, `loadAuth`, `clearAuth`, `isAuthenticated`).
- `toastStore` — `success / error / info / warning` messages consumed by `AppToastHost`.

## Routing

[`src/router.ts`](./src/router.ts) uses a `beforeEach` guard:

- Unauthenticated access to a non-public route → redirect to `/login`.
- Authenticated user on `/login` or `/register` → redirect to `/dashboard`.

All authenticated routes are rendered inside `AppShell`. The sitemap (sidebar + route tree) is defined in [`src/sitemap.ts`](./src/sitemap.ts) and uses `nav.*` i18n keys for labels.

## List page pattern

All CRUD list pages follow the same pattern (see `views/configuration/*View.vue`):

1. `useCrudPage<TItem, TFilters>({ fetch })` from `src/composables/useCrudPage.ts` owns page / size / sort / filters / items / loading.
2. `AppPageHeader` + `AppFilterBar` + `AppTable` + `AppPagination`.
3. `AppModal` for create/edit, `AppConfirmDialog` for delete, `useToastStore` for outcome feedback, `extractErrorMessage` for error messages.
4. List fetch failures surface through `AppTable`'s `error` prop + `retry` event (wired to `table.error` / `table.retry`); custom regions use `AppDataState` (`loading` / `error` / `empty` + `retry`) or `AppErrorState` directly. Never invent a one-off error banner.

## Navigation

- Sidebar entries come from [`src/sitemap.ts`](./src/sitemap.ts); group items carry their base `route` so the group highlights on overview routes (e.g. `/settings`).
- Active-state matching lives in [`src/utils/navigation.ts`](./src/utils/navigation.ts) (`isNavRouteActive`, `isNavGroupActive`, `findActiveNavTrail`) — segment-aware, so detail pages (`/production/orders/:id`) highlight their browse parent without cross-matching unrelated prefixes.
- Unknown routes render the guarded `NotFoundView` inside `AppShell` (auth + permission guards apply first) instead of silently bouncing to the dashboard.

## i18n

- Default locale: **Polish (`pl`)**. Fallback: **English (`en`)**.
- Selected locale is persisted to `localStorage['locale']` and switchable from the top bar.
- All strings live in [`src/i18n.ts`](./src/i18n.ts) — no hard-coded user-facing strings in components.
