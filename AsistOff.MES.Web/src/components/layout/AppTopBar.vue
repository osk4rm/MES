<template>
  <header class="app-topbar">
    <div class="app-topbar__left">
      <div class="app-topbar__env" :title="envTitle">
        <span class="app-topbar__env-dot" aria-hidden="true"></span>
        <span class="app-topbar__env-label">{{ envLabel }}</span>
      </div>
    </div>
    <div class="app-topbar__right">
      <button class="app-topbar__icon-btn" :title="$t('common.notifications')" type="button" aria-label="Notifications">
        <i class="pi pi-bell"></i>
      </button>
      <div class="app-topbar__divider" aria-hidden="true"></div>
      <div class="app-topbar__lang">
        <button
          v-for="l in locales"
          :key="l.code"
          type="button"
          :class="['app-topbar__lang-btn', { 'app-topbar__lang-btn--active': currentLocale === l.code }]"
          @click="setLocale(l.code)"
        >
          {{ l.label }}
        </button>
      </div>
      <div class="app-topbar__divider" aria-hidden="true"></div>
      <div class="app-topbar__user">
        <div class="app-topbar__avatar" aria-hidden="true">
          <i class="pi pi-user"></i>
        </div>
        <div v-if="user?.email" class="app-topbar__user-info">
          <span class="app-topbar__user-name">{{ user.email }}</span>
          <span class="app-topbar__user-role">{{ $t('common.signedIn') }}</span>
        </div>
        <button type="button" class="app-topbar__icon-btn" :title="$t('common.signOut')" :aria-label="$t('common.signOut')" @click="emit('sign-out')">
          <i class="pi pi-sign-out"></i>
        </button>
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';

const props = defineProps<{ user?: { email?: string } | null }>();
void props;

const emit = defineEmits<{ (e: 'sign-out'): void }>();

const i18n = useI18n();
const locales = [
  { code: 'pl', label: 'PL' },
  { code: 'en', label: 'EN' }
];
const currentLocale = computed(() => i18n.locale.value);

function setLocale(code: string) {
  i18n.locale.value = code;
  try { localStorage.setItem('locale', code); } catch {}
}

const envLabel = computed(() => (import.meta.env.DEV ? 'DEV' : 'PROD'));
const envTitle = computed(() => `Environment: ${envLabel.value}`);
</script>

<style scoped>
.app-topbar {
  grid-area: topbar;
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: var(--layout-topbar-height);
  padding: 0 var(--space-4);
  background: var(--color-surface);
  border-bottom: 1px solid var(--color-border);
}

.app-topbar__env {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  padding: 2px var(--space-2);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  font-size: var(--font-size-xs);
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.app-topbar__env-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--color-success);
}

.app-topbar__right { display: flex; align-items: center; gap: var(--space-2); }

.app-topbar__icon-btn {
  width: 32px;
  height: 32px;
  border-radius: var(--radius-md);
  color: var(--color-text-muted);
  display: inline-flex;
  align-items: center;
  justify-content: center;
}
.app-topbar__icon-btn:hover { background: var(--color-surface-sunken); color: var(--color-text); }

.app-topbar__divider { width: 1px; height: 20px; background: var(--color-divider); }

.app-topbar__lang { display: inline-flex; align-items: center; border: 1px solid var(--color-border); border-radius: var(--radius-sm); overflow: hidden; }
.app-topbar__lang-btn {
  padding: 0 var(--space-2);
  height: 24px;
  font-size: var(--font-size-xs);
  font-weight: var(--font-weight-semibold);
  color: var(--color-text-muted);
}
.app-topbar__lang-btn:hover { background: var(--color-surface-sunken); }
.app-topbar__lang-btn--active { background: var(--color-primary); color: var(--color-text-inverse); }

.app-topbar__user { display: flex; align-items: center; gap: var(--space-2); }
.app-topbar__avatar {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  background: var(--color-primary-soft);
  color: var(--color-primary);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
}
.app-topbar__user-info { display: flex; flex-direction: column; line-height: 1.1; }
.app-topbar__user-name { font-size: var(--font-size-sm); font-weight: var(--font-weight-medium); }
.app-topbar__user-role { font-size: 10px; color: var(--color-text-subtle); text-transform: uppercase; letter-spacing: 0.05em; }
</style>
