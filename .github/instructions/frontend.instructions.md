---
applyTo: "AsistOff.MES.Web/src/**"
---

# Frontend – Vue 3 / TypeScript

## Stack

| Tool | Version / Notes |
|------|-----------------|
| Vue 3 | Composition API, `<script setup>` |
| TypeScript | `~5.8` |
| Build tool | Vite 7 |
| Router | Vue Router 4 – config in `src/router.ts` |
| State | Pinia 3 – stores in `src/stores/` |
| UI library | PrimeVue 4 + PrimeIcons |
| HTTP | Axios – service wrappers in `src/services/` |
| i18n | vue-i18n 11 – setup in `src/i18n.ts` |
| Toasts | vue-toastification – helper in `src/toast.ts` |

## SFC Conventions

- Always use `<script setup lang="ts">`.
- Define props with `defineProps<{ ... }>()` and emits with `defineEmits<{ ... }>()`.
- Prefer `ref` and `computed`; avoid `reactive` for simple values.
- Extract shared logic into composables (`src/composables/`).
- Keep template logic minimal – move complex expressions to `computed` properties.

```vue
<script setup lang="ts">
import { ref, computed } from 'vue'

const props = defineProps<{ title: string; isActive?: boolean }>()
const emit = defineEmits<{ (e: 'close'): void }>()

const label = computed(() => props.isActive ? 'Active' : 'Inactive')
</script>
```

## Design System – Industrial Components

Use the custom industrial-themed component set (documented in `AsistOff.MES.Web/COMPONENTS.md`). **Never use raw HTML elements where a component exists.**

| Component | Use for |
|-----------|---------|
| `IndustrialButton` | Every button – never plain `<button>` |
| `IndustrialInput` | Every text / form input |
| `PageHeader` | Top of every page view |
| `FilterBar` | Wrapper for filter inputs |
| `ActionButtons` | Row-level actions in data grids |
| `StatusBadge` | Any status indicator |
| `WidgetCard` | Dashboard metric cards |
| `ConfirmDialog` | Destructive action confirmation |
| `DataTable` / `DataGrid` | Tabular data |

## Design Tokens & Styles

CSS variables and base styles are in `src/style.css`. Do **not** hardcode hex colors inline.

| Purpose | Palette |
|---------|---------|
| Primary accent | Purple gradients (`#6366f1` → `#8b5cf6`) |
| Background | Dark slate (`#1e293b` → `#334155`) |
| Text – primary | `#f1f5f9` |
| Text – muted | `#94a3b8` |
| Success / Warning / Danger | Standard semantic colors |

## State Management (Pinia)

- Store files live in `src/stores/`.
- Use `defineStore` with the Composition API style (not Options API).
- Store IDs use camelCase (e.g., `'authStore'`).
- JWT token and user data are managed in `authStore.ts`.

## API Communication

- All HTTP calls are wrapped in service files in `src/services/`.
- Use the Axios instance configured there (do not create ad-hoc `axios` calls in components).
- Handle errors at the service level and surface them via toast notifications.

## Routing

- Routes are declared in `src/router.ts`.
- Use **named routes** for `router.push` / `<RouterLink>`.
- Protect authenticated routes with navigation guards (check `authStore.isAuthenticated`).

## Internationalization

- Use `vue-i18n` for all user-visible strings; never hardcode UI text.
- Access translations with `const { t } = useI18n()` in `<script setup>`.
- Translation keys are organized by feature / view.

## Models

- TypeScript interfaces and types for API responses live in `src/models/`.
- Match backend DTO field names (camelCase from the JSON serializer).

## File & Folder Naming

| Asset | Convention |
|-------|------------|
| Components | `PascalCase.vue` |
| Composables | `use{Name}.ts` |
| Stores | `{name}Store.ts` |
| Services | `{name}Service.ts` |
| Views | `{Name}View.vue` |
