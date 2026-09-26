<template>
  <aside :class="['app-sidenav', { 'app-sidenav--collapsed': collapsed }]">
    <div class="app-sidenav__brand">
      <div class="app-sidenav__logo" aria-hidden="true">
        <span>MES</span>
      </div>
      <div v-if="!collapsed" class="app-sidenav__brand-text">
        <span class="app-sidenav__brand-name">AsistOff</span>
        <span class="app-sidenav__brand-sub">Manufacturing Execution</span>
      </div>
    </div>

    <nav class="app-sidenav__nav" aria-label="Main">
      <ul>
        <li v-for="item in items" :key="item.label" class="app-sidenav__group">
          <template v-if="!item.children">
            <router-link
              :to="item.route!"
              :class="['app-sidenav__link', { 'app-sidenav__link--active': isActive(item.route!) }]"
              :aria-current="isActive(item.route!) ? 'page' : undefined"
              :title="collapsed ? $t(item.label) : undefined"
            >
              <i :class="['app-sidenav__icon', item.icon]" aria-hidden="true"></i>
              <span v-if="!collapsed" class="app-sidenav__label">{{ $t(item.label) }}</span>
            </router-link>
          </template>
          <template v-else>
            <button
              type="button"
              :class="['app-sidenav__link', 'app-sidenav__link--group', { 'app-sidenav__link--open': isOpen(item.label), 'app-sidenav__link--active': isGroupActive(item) }]"
              :title="collapsed ? $t(item.label) : undefined"
              :aria-expanded="!collapsed && isOpen(item.label)"
              @click="toggle(item.label)"
            >
              <i :class="['app-sidenav__icon', item.icon]" aria-hidden="true"></i>
              <span v-if="!collapsed" class="app-sidenav__label">{{ $t(item.label) }}</span>
              <i v-if="!collapsed" :class="['app-sidenav__chevron pi', isOpen(item.label) ? 'pi-angle-down' : 'pi-angle-right']" aria-hidden="true"></i>
            </button>
            <ul v-if="!collapsed && isOpen(item.label)" class="app-sidenav__subnav">
              <li v-for="sub in item.children" :key="sub.label">
                <router-link
                  :to="sub.route!"
                  :class="['app-sidenav__link', 'app-sidenav__link--sub', { 'app-sidenav__link--active': isActive(sub.route!) }]"
                  :aria-current="isActive(sub.route!) ? 'page' : undefined"
                >
                  <i :class="['app-sidenav__icon app-sidenav__icon--sub', sub.icon]" aria-hidden="true"></i>
                  <span class="app-sidenav__label">{{ $t(sub.label) }}</span>
                </router-link>
              </li>
            </ul>
          </template>
        </li>
      </ul>
    </nav>

    <button type="button" class="app-sidenav__toggle" @click="emit('update:collapsed', !collapsed)" :aria-label="collapsed ? 'Expand sidebar' : 'Collapse sidebar'">
      <i :class="['pi', collapsed ? 'pi-angle-double-right' : 'pi-angle-double-left']"></i>
    </button>
  </aside>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { useRoute } from 'vue-router';
import type { NavItem } from '../../sitemap';
import { isNavGroupActive, isNavRouteActive } from '../../utils/navigation';

const props = defineProps<{ items: NavItem[]; collapsed: boolean }>();
const emit = defineEmits<{ (e: 'update:collapsed', value: boolean): void }>();

const route = useRoute();
const manualOpen = ref<Record<string, boolean>>({});

// Active state is segment-aware (issue #314): detail pages highlight their
// browse parent, and `/production/telemetry` stays inactive while on
// `/production/telemetry-dashboard`.
const currentPath = computed(() => route.path);

function isActive(itemRoute: string): boolean {
  return isNavRouteActive(currentPath.value, itemRoute);
}

function isGroupActive(group: NavItem): boolean {
  return isNavGroupActive(currentPath.value, group);
}

function toggle(label: string) {
  manualOpen.value = { ...manualOpen.value, [label]: !isOpen(label) };
}

function isOpen(label: string) {
  if (label in manualOpen.value) return manualOpen.value[label];
  const item = props.items.find(i => i.label === label);
  return item ? isNavGroupActive(currentPath.value, item) : false;
}
</script>

<style scoped>
.app-sidenav {
  grid-area: sidenav;
  background: var(--color-surface);
  border-right: 1px solid var(--color-border);
  display: flex;
  flex-direction: column;
  width: var(--layout-sidenav-width);
  transition: width var(--transition-base);
  position: relative;
  z-index: 10;
}

.app-sidenav--collapsed { width: var(--layout-sidenav-collapsed); }

.app-sidenav__brand {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-4);
  border-bottom: 1px solid var(--color-divider);
  min-height: calc(var(--layout-topbar-height) + 1px);
}

.app-sidenav__logo {
  width: 32px;
  height: 32px;
  background: var(--color-primary);
  color: var(--color-text-inverse);
  border-radius: var(--radius-md);
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: var(--font-weight-bold);
  font-size: 11px;
  letter-spacing: 0.05em;
  flex-shrink: 0;
}

.app-sidenav__brand-text { display: flex; flex-direction: column; min-width: 0; }
.app-sidenav__brand-name { font-weight: var(--font-weight-semibold); font-size: var(--font-size-md); color: var(--color-text); }
.app-sidenav__brand-sub { font-size: 10px; color: var(--color-text-subtle); text-transform: uppercase; letter-spacing: 0.05em; }

.app-sidenav__nav {
  flex: 1;
  overflow-y: auto;
  padding: var(--space-3) var(--space-2);
}

.app-sidenav__nav > ul { display: flex; flex-direction: column; gap: 2px; }

.app-sidenav__link {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-2) var(--space-3);
  color: var(--color-text-muted);
  border-radius: var(--radius-md);
  font-size: var(--font-size-md);
  font-weight: var(--font-weight-medium);
  width: 100%;
  text-align: left;
  transition: background var(--transition-fast), color var(--transition-fast);
  cursor: pointer;
  border: 0;
  background: transparent;
}

.app-sidenav--collapsed .app-sidenav__link { justify-content: center; padding: var(--space-2); }

.app-sidenav__link:hover { background: var(--color-surface-sunken); color: var(--color-text); text-decoration: none; }
.app-sidenav__link.router-link-active,
.app-sidenav__link--active { background: var(--color-primary-soft); color: var(--color-primary); }

.app-sidenav__icon { width: 16px; font-size: 14px; flex-shrink: 0; text-align: center; }
.app-sidenav__icon--sub { font-size: 12px; }

.app-sidenav__label { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

.app-sidenav__chevron { font-size: 10px; color: var(--color-text-subtle); }

.app-sidenav__subnav { margin: 2px 0 var(--space-2) var(--space-6); padding-left: var(--space-3); border-left: 1px solid var(--color-divider); display: flex; flex-direction: column; gap: 2px; }

.app-sidenav__link--sub { font-weight: var(--font-weight-regular); padding: var(--space-1) var(--space-2); font-size: var(--font-size-sm); }

.app-sidenav__toggle {
  align-self: center;
  margin: var(--space-3) 0;
  width: 28px;
  height: 28px;
  border-radius: var(--radius-full);
  color: var(--color-text-subtle);
  border: 1px solid var(--color-border);
  display: inline-flex;
  align-items: center;
  justify-content: center;
}
.app-sidenav__toggle:hover { background: var(--color-surface-sunken); color: var(--color-text); }
</style>
