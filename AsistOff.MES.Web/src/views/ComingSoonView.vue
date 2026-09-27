<template>
  <div>
    <AppPageHeader :title="title" :icon="icon" />
    <AppCard v-if="isSettingsIndex">
      <p class="settings-index__hint">{{ $t('settings.indexHint') }}</p>
      <ul v-if="settingsLinks.length > 0" class="settings-index__links">
        <li v-for="link in settingsLinks" :key="link.to">
          <router-link :to="link.to" class="settings-index__link">
            <i :class="['settings-index__icon', link.icon]" aria-hidden="true"></i>
            {{ $t(link.labelKey) }}
          </router-link>
        </li>
      </ul>
      <AppEmptyState v-else icon="pi pi-wrench" :title="$t('stubs.title')" :description="$t('stubs.description')" />
    </AppCard>
    <AppCard v-else>
      <AppEmptyState icon="pi pi-wrench" :title="$t('stubs.title')" :description="$t('stubs.description')" />
    </AppCard>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import { useI18n } from 'vue-i18n';
import AppPageHeader from '../components/ui/AppPageHeader.vue';
import AppCard from '../components/ui/AppCard.vue';
import AppEmptyState from '../components/ui/AppEmptyState.vue';
import { useAuthStore } from '../stores/authStore';
import { Permissions } from '../models/authModels';

const route = useRoute();
const { t } = useI18n();
const auth = useAuthStore();
const title = computed(() => {
  const key = route.meta?.titleKey as string | undefined;
  return key ? t(key) : '';
});
const icon = computed(() => (route.meta?.icon as string | undefined) ?? 'pi pi-wrench');

// The `/settings` overview stub (F-17) links onward to the settings sections
// that exist — filtered by the same permission grants as the sidebar, so a
// non-admin never gets a link that would only bounce with access-denied.
const isSettingsIndex = computed(() => route.name === 'settings');

interface SettingsLink {
  to: string;
  labelKey: string;
  icon: string;
  permission?: string;
}

const allSettingsLinks: SettingsLink[] = [
  { to: '/settings/roles', labelKey: 'nav.roles', icon: 'pi pi-lock', permission: Permissions.TenantAdmin }
];

const settingsLinks = computed<SettingsLink[]>(() =>
  allSettingsLinks.filter((link) => link.permission === undefined || auth.hasPermission(link.permission))
);
</script>

<style scoped>
.settings-index__hint {
  color: var(--color-text-muted);
  font-size: var(--font-size-md);
  margin: 0 0 var(--space-3);
}
.settings-index__links {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}
.settings-index__link {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-3) var(--space-4);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  color: var(--color-text);
  font-weight: var(--font-weight-medium);
}
.settings-index__link:hover {
  background: var(--color-surface-sunken);
  text-decoration: none;
}
.settings-index__icon {
  color: var(--color-primary);
}
</style>
