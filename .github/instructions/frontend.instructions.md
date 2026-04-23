---
applyTo: "AsistOff.MES.Web/src/**"
---

# Frontend – Vue 3 / TypeScript

## Stack

| Tool | Notes |
|------|-------|
| Vue 3 | Composition API only, `<script setup lang="ts">` |
| TypeScript | `~5.8`, `strict`, `noUnusedLocals`, `noUnusedParameters`, `erasableSyntaxOnly` — no `enum`, use `const` objects + derived types |
| Build tool | Vite 7 (`vue-tsc -b && vite build`) |
| Router | Vue Router 4 – config in `src/router.ts`, auth guard in `beforeEach` |
| State | Pinia 3 – stores in `src/stores/` |
| HTTP | Axios – service wrappers in `src/services/`, never `import axios` directly in components |
| i18n | vue-i18n 11 – default `pl`, fallback `en`, all messages in `src/i18n.ts` |
| Icons | `primeicons` (icon font only — no UI framework) |

**No UI framework runtime dep.** `primevue` and `vue-toastification` are removed. Do not reintroduce third-party UI/toast libraries — use the in-house design system.

## SFC conventions

- Always `<script setup lang="ts">`; define props with `defineProps<{...}>()` and emits with `defineEmits<{...}>()`.
- Prefer `ref` + `computed`; use `reactive` only for form state objects.
- Share logic via composables (`src/composables/`), e.g. `useCrudPage` for list pages.
- Keep templates declarative — push logic to `computed`.

## Design system (`src/components/ui/`)

Always use the `App*` components instead of raw HTML. The full list and usage pattern is in [`AsistOff.MES.Web/COMPONENTS.md`](../../AsistOff.MES.Web/COMPONENTS.md). Key rules:

- **Buttons:** `AppButton` (never raw `<button>`).
- **Form controls:** `AppInput`, `AppNumberInput`, `AppTextarea`, `AppSelect`, `AppCheckbox` — wrap each with `AppFormField` for label + error.
- **Dialogs:** `AppModal`, `AppConfirmDialog` (for destructive actions).
- **Feedback:** `AppBadge` (statuses), `AppSpinner`, `AppEmptyState`, `AppToastHost` (mount once in `App.vue`).
- **Layout:** `AppPageHeader` at the top of every page, `AppCard` for grouped content, `AppShell` owns routing layout.
- **Lists:** `AppFilterBar` + `AppTable` + `AppPagination` + `AppRowActions`.

Never hard-code hex values. Reference CSS tokens defined in `src/style.css` (`--color-*`, `--space-*`, `--radius-*`, `--font-*`, `--layout-*`, `--control-height-*`, `--transition-*`).

Palette is steel-navy primary (`--color-primary` ≈ `#1E3A5F`), amber reserved for warnings (`--color-warning`), semantic success/danger/idle for statuses. Light theme is the default; dark theme is enabled via `data-theme="dark"`.

## State management (Pinia)

- Store files in `src/stores/`, one `defineStore` per file using the options style.
- `authStore` exposes `token`, `user`, `isAuthenticated`, `setAuth`, `loadAuth`, `clearAuth` — persisted to `localStorage`.
- `toastStore` drives `AppToastHost`; call `toastStore.success|error|info|warning(message)` instead of importing notification libs.

## API communication

- Every HTTP call goes through a `src/services/*Service.ts` wrapper that uses the shared `http` axios instance.
- The shared `http` interceptor injects the `Authorization` header and redirects to `/login` on `401`.
- Use `extractErrorMessage(err, fallback)` from `src/services/http.ts` to produce toast-ready error strings that honour ASP.NET Core `ProblemDetails` / `ValidationProblemDetails`.
- Paged list requests use `buildPagedParams(req)` from `tenantService.ts` to strip empty filter values.

## Routing

- Authenticated routes live under the `AppShell` layout route.
- Public routes (`/login`, `/register`) must set `meta.public = true`; the `beforeEach` guard handles redirects.
- Sidebar entries and route structure must stay in sync with `src/sitemap.ts` (keys come from the `nav.*` i18n namespace).

## Internationalization

- All user-visible strings live in `src/i18n.ts` under nested keys grouped by feature (`products.*`, `operators.*`, `common.*`, …). **Never hardcode UI text in components.**
- Default locale is `pl`; English is the fallback. Users switch locale from the top bar; it persists to `localStorage['locale']`.

## List/CRUD page pattern

1. Call `useCrudPage<TItem, TFilters>({ fetch: req => service.browse(req) })` from `src/composables/useCrudPage.ts` — it owns page, size, sort, filters, items, loading.
2. Render `AppPageHeader` (with action buttons) → `AppFilterBar` (debounced text filters, 300 ms) → `AppTable` + `AppPagination`.
3. Create/edit uses `AppModal` with the row's form. Delete uses `AppConfirmDialog`. Feedback uses `useToastStore()` and `extractErrorMessage`.
4. Lookups for dropdowns (e.g. product groups, departments) load once via the same service in `onMounted`.

## File & folder naming

| Asset | Convention |
|-------|------------|
| Primitive components | `App*.vue` in `src/components/ui/` |
| Layout components | `App*.vue` in `src/components/layout/` |
| Composables | `use{Name}.ts` |
| Stores | `{name}Store.ts` |
| Services | `{name}Service.ts` |
| Views | `{Name}View.vue` — configuration modules live in `src/views/configuration/` |
