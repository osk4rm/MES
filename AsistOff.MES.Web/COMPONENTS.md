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
| `AppButton`        | Primary / secondary / ghost / danger buttons; icons, loading |
| `AppInput`         | Text / email / password input with prefix icon          |
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
| `AppBadge`         | Status chip (semantic variants)                         |
| `AppCard`          | Content block with optional header/footer               |
| `AppPageHeader`    | Standard page header with breadcrumbs and actions slot  |
| `AppBreadcrumbs`   | Breadcrumb trail                                        |
| `AppEmptyState`    | Empty/Coming-soon placeholder                           |
| `AppSpinner`       | Minimal CSS spinner                                     |

Layout components live in `src/components/layout/`:

| Component    | Purpose                                      |
|--------------|----------------------------------------------|
| `AppShell`   | Grid layout wrapping sidenav + topbar + main |
| `AppSideNav` | Collapsible navigation from `sitemap.ts`     |
| `AppTopBar`  | User menu, locale switcher, env indicator    |

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

## i18n

- Default locale: **Polish (`pl`)**. Fallback: **English (`en`)**.
- Selected locale is persisted to `localStorage['locale']` and switchable from the top bar.
- All strings live in [`src/i18n.ts`](./src/i18n.ts) — no hard-coded user-facing strings in components.
