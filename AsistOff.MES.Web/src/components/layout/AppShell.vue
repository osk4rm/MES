<template>
  <div class="app-shell">
    <AppSideNav v-model:collapsed="collapsed" :items="sitemap" />
    <AppTopBar :user="authStore.user" @sign-out="onSignOut" />
    <main class="app-shell__main">
      <router-view />
    </main>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import AppSideNav from './AppSideNav.vue';
import AppTopBar from './AppTopBar.vue';
import { sitemap } from '../../sitemap';
import { useAuthStore } from '../../stores/authStore';
import { signOut } from '../../services/authService';

const router = useRouter();
const authStore = useAuthStore();

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
</style>
