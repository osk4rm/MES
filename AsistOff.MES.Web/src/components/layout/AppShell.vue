<template>
  <div class="app-shell">
    <AppSideNav v-model:collapsed="collapsed" :items="sitemap" />
    <AppTopBar :user="authStore.user" @sign-out="onSignOut" />
    <main class="app-shell__main">
      <div class="app-shell__crumbs">
        <AppBreadcrumbs :items="crumbs" />
      </div>
      <AppErrorBoundary>
        <router-view />
      </AppErrorBoundary>
    </main>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import AppSideNav from './AppSideNav.vue';
import AppTopBar from './AppTopBar.vue';
import AppBreadcrumbs, { type Crumb } from '../ui/AppBreadcrumbs.vue';
import AppErrorBoundary from '../ui/AppErrorBoundary.vue';
import { sitemap } from '../../sitemap';
import { findActiveNavTrail } from '../../utils/navigation';
import { deepestTitleKey, documentTitleForTitleKey } from '../../navigationMap';
import { useAuthStore } from '../../stores/authStore';
import { signOut } from '../../services/authService';

const router = useRouter();
const route = useRoute();
const authStore = useAuthStore();
const { t, locale } = useI18n();

// Central breadcrumb trail (issue #337): derived from the sitemap for the
// current path, so every shell view — including detail pages, which resolve
// to their browse parent, and the not-found view — shows a trail consistent
// with the published navigation map without per-view wiring.
const crumbs = computed<Crumb[]>(() => {
  const trail = findActiveNavTrail(sitemap, route.path);
  if (trail.length === 0) return [{ label: t('notFound.title') }];
  return trail.map((item, idx): Crumb => {
    if (idx < trail.length - 1 && item.route) return { label: t(item.label), to: item.route };
    return { label: t(item.label) };
  });
});

// Re-apply the document title when the locale changes (issue #337): the
// router afterEach only fires on navigation, so a PL↔EN switch on a
// stationary page would otherwise leave a stale tab title.
function applyDocumentTitle(): void {
  try {
    document.title = documentTitleForTitleKey((key: string) => t(key), deepestTitleKey(route.matched));
  } catch { /* ignore - document is unavailable in some test hosts */ }
}

watch(() => route.fullPath, applyDocumentTitle, { immediate: true });
watch(locale, applyDocumentTitle);

const collapsed = ref<boolean>(loadCollapsed());

function loadCollapsed() {
  try { return localStorage.getItem('sidenav.collapsed') === '1'; } catch { return false; }
}

watch(collapsed, (v) => {
  try { localStorage.setItem('sidenav.collapsed', v ? '1' : '0'); } catch { /* ignore */ }
});

async function onSignOut() {
  // Best-effort server sign-out so httpOnly cookies are cleared and the
  // refresh token is revoked; local state is cleared either way.
  try {
    await signOut();
  } catch { /* ignore - local state is cleared below */ }
  authStore.clearAuth();
  await router.push('/login');
}
</script>

<style scoped>
.app-shell {
  display: grid;
  grid-template-areas:
    'sidenav topbar'
    'sidenav main';
  grid-template-columns: auto 1fr;
  grid-template-rows: var(--layout-topbar-height) 1fr;
  height: 100vh;
  width: 100vw;
  background: var(--color-bg);
}

.app-shell__main {
  grid-area: main;
  overflow: auto;
  padding: var(--space-6);
}

.app-shell__crumbs {
  margin-bottom: var(--space-3);
}
</style>
