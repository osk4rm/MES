<template>
  <div class="auth-page">
    <div class="auth-page__split">
      <div class="auth-page__panel">
        <div class="auth-page__brand">
          <div class="auth-page__logo">MES</div>
          <div>
            <div class="auth-page__brand-name">AsistOff</div>
            <div class="auth-page__brand-sub">Manufacturing Execution System</div>
          </div>
        </div>
        <h1 class="auth-page__title">{{ $t('auth.signInTitle') }}</h1>
        <p class="auth-page__subtitle">{{ $t('auth.signInSubtitle') }}</p>

        <form class="auth-page__form" data-testid="login-form" @submit.prevent="onSubmit">
          <AppFormField :label="$t('auth.email')" required>
            <template #default="{ id, invalid }">
              <AppInput
                :id="id"
                v-model="email"
                type="email"
                autocomplete="username"
                :invalid="invalid"
                required
                prefix-icon="pi pi-envelope"
                data-testid="login-email"
              />
            </template>
          </AppFormField>

          <AppFormField :label="$t('auth.password')" required>
            <template #default="{ id, invalid }">
              <AppInput
                :id="id"
                v-model="password"
                type="password"
                autocomplete="current-password"
                :invalid="invalid"
                required
                prefix-icon="pi pi-lock"
                data-testid="login-password"
              />
            </template>
          </AppFormField>

          <AppButton type="submit" variant="primary" size="lg" block :loading="loading" data-testid="login-submit">
            {{ $t('auth.signIn') }}
          </AppButton>

          <button type="button" class="auth-page__link" @click="goRegister">
            {{ $t('auth.goToRegister') }}
          </button>
        </form>
      </div>

      <aside class="auth-page__side" aria-hidden="true">
        <div class="auth-page__side-inner">
          <div class="auth-page__side-kicker">AsistOff MES</div>
          <h2 class="auth-page__side-title">Enterprise-grade shop-floor control.</h2>
          <p class="auth-page__side-text">Orders, recipes, operators, warehouses — one tenant-isolated workspace.</p>
        </div>
      </aside>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { useI18n } from 'vue-i18n';
import AppInput from '../components/ui/AppInput.vue';
import AppFormField from '../components/ui/AppFormField.vue';
import AppButton from '../components/ui/AppButton.vue';
import { signIn } from '../services/authService';
import { resolveSafeRedirect } from '../composables/useSafeRedirect';
import { useAuthStore } from '../stores/authStore';
import { useToastStore } from '../stores/toastStore';
import { extractErrorMessage } from '../services/http';

const email = ref('');
const password = ref('');
const loading = ref(false);
const router = useRouter();
const route = useRoute();
const authStore = useAuthStore();
const toast = useToastStore();
const { t } = useI18n();

async function onSubmit() {
  loading.value = true;
  try {
    // Cookie transport (issue #242): the session arrives via httpOnly
    // Set-Cookie and the body tokens are intentionally empty. A 200
    // means the cookies were issued — only the display email is kept,
    // in memory, and no token is written to any storage.
    await signIn({ email: email.value, password: password.value });
    authStore.setAuth({ email: email.value });
    toast.success(t('auth.signInSuccess'));
    const redirect = route.query['redirect'];
    await router.push(resolveSafeRedirect(redirect));
  } catch (err) {
    toast.error(extractErrorMessage(err, t('auth.signInError')));
  } finally {
    loading.value = false;
  }
}

function goRegister() {
  // Carry the post-login target through registration so the chain
  // register -> login lands where the caller intended (issue #242).
  router.push({ path: '/register', query: route.query });
}
</script>

<style scoped>
.auth-page {
  min-height: 100vh;
  display: flex;
  align-items: stretch;
  background: var(--color-bg);
}

.auth-page__split {
  display: grid;
  grid-template-columns: 1fr 1fr;
  width: 100%;
  max-width: 1200px;
  margin: 0 auto;
}

.auth-page__panel {
  display: flex;
  flex-direction: column;
  justify-content: center;
  padding: var(--space-12) var(--space-10);
  gap: var(--space-4);
}

.auth-page__brand {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  margin-bottom: var(--space-6);
}

.auth-page__logo {
  width: 40px;
  height: 40px;
  background: var(--color-primary);
  color: var(--color-text-inverse);
  border-radius: var(--radius-md);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-weight: var(--font-weight-bold);
  font-size: 13px;
  letter-spacing: 0.05em;
}

.auth-page__brand-name { font-weight: var(--font-weight-semibold); font-size: var(--font-size-lg); color: var(--color-text); }
.auth-page__brand-sub { color: var(--color-text-subtle); font-size: var(--font-size-sm); }

.auth-page__title { font-size: var(--font-size-2xl); color: var(--color-text); }
.auth-page__subtitle { color: var(--color-text-muted); margin-bottom: var(--space-4); }

.auth-page__form {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  max-width: 380px;
}

.auth-page__link {
  color: var(--color-primary);
  font-size: var(--font-size-sm);
  text-align: center;
  padding: var(--space-2);
}
.auth-page__link:hover { text-decoration: underline; }

.auth-page__side {
  background: linear-gradient(135deg, #1e3a5f 0%, #0f1f30 100%);
  color: var(--color-text-inverse);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: var(--space-12);
}

.auth-page__side-inner { max-width: 380px; }
.auth-page__side-kicker { font-size: var(--font-size-xs); letter-spacing: 0.15em; text-transform: uppercase; color: rgba(255,255,255,0.55); margin-bottom: var(--space-4); }
.auth-page__side-title { font-size: var(--font-size-3xl); line-height: 1.15; color: #fff; margin-bottom: var(--space-3); }
.auth-page__side-text { color: rgba(255,255,255,0.75); font-size: var(--font-size-lg); }

@media (max-width: 900px) {
  .auth-page__split { grid-template-columns: 1fr; }
  .auth-page__side { display: none; }
}
</style>
