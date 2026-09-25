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
        <h1 class="auth-page__title">{{ $t('auth.signUpTitle') }}</h1>
        <p class="auth-page__subtitle">{{ $t('auth.signUpSubtitle') }}</p>

        <form class="auth-page__form" @submit.prevent="onSubmit">
          <AppFormField :label="$t('auth.tenantName')" required>
            <template #default="{ id, invalid }">
              <AppInput :id="id" v-model="form.name" required :invalid="invalid" prefix-icon="pi pi-building" />
            </template>
          </AppFormField>

          <AppFormField :label="$t('auth.tenantDisplayName')" required>
            <template #default="{ id, invalid }">
              <AppInput :id="id" v-model="form.displayName" required :invalid="invalid" />
            </template>
          </AppFormField>

          <AppFormField :label="$t('auth.contactEmail')" required>
            <template #default="{ id, invalid }">
              <AppInput :id="id" v-model="form.contactEmail" type="email" autocomplete="email" required :invalid="invalid" prefix-icon="pi pi-envelope" />
            </template>
          </AppFormField>

          <AppFormField :label="$t('auth.password')" required>
            <template #default="{ id, invalid }">
              <AppInput :id="id" v-model="form.password" type="password" autocomplete="new-password" required :invalid="invalid" prefix-icon="pi pi-lock" />
            </template>
          </AppFormField>

          <AppFormField :label="$t('auth.confirmPassword')" required :error="confirmError">
            <template #default="{ id, invalid }">
              <AppInput :id="id" v-model="form.confirmPassword" type="password" autocomplete="new-password" required :invalid="invalid" prefix-icon="pi pi-lock" />
            </template>
          </AppFormField>

          <AppButton type="submit" variant="primary" size="lg" block :loading="loading">
            {{ $t('auth.register') }}
          </AppButton>
          <button type="button" class="auth-page__link" @click="goLogin">{{ $t('auth.backToLogin') }}</button>
        </form>
      </div>

      <aside class="auth-page__side" aria-hidden="true">
        <div class="auth-page__side-inner">
          <div class="auth-page__side-kicker">Multi-tenant</div>
          <h2 class="auth-page__side-title">Isolated workspace for your organization.</h2>
          <p class="auth-page__side-text">Every tenant gets a dedicated, strictly-isolated dataset with its own admin account.</p>
        </div>
      </aside>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { useI18n } from 'vue-i18n';
import AppInput from '../components/ui/AppInput.vue';
import AppFormField from '../components/ui/AppFormField.vue';
import AppButton from '../components/ui/AppButton.vue';
import { createTenant } from '../services/tenantService';
import { useToastStore } from '../stores/toastStore';
import { extractErrorMessage } from '../services/http';

const form = reactive({
  name: '',
  displayName: '',
  contactEmail: '',
  password: '',
  confirmPassword: ''
});

const loading = ref(false);
const router = useRouter();
const route = useRoute();
const toast = useToastStore();
const { t } = useI18n();

const confirmError = computed(() =>
  form.confirmPassword && form.confirmPassword !== form.password ? t('auth.passwordsMismatch') : null
);

async function onSubmit() {
  if (form.password !== form.confirmPassword) {
    toast.error(t('auth.passwordsMismatch'));
    return;
  }
  loading.value = true;
  try {
    await createTenant({
      name: form.name,
      displayName: form.displayName,
      contactEmail: form.contactEmail,
      password: form.password,
      confirmPassword: form.confirmPassword,
      settings: ''
    });
    toast.success(t('auth.registerSuccess'));
    // Forward the post-login target (if any) so the register -> login
    // chain lands where the caller intended (issue #242).
    await router.push({ path: '/login', query: route.query });
  } catch (err) {
    toast.error(extractErrorMessage(err, t('auth.registerError')));
  } finally {
    loading.value = false;
  }
}

function goLogin() { router.push({ path: '/login', query: route.query }); }
</script>

<style scoped>
.auth-page { min-height: 100vh; display: flex; background: var(--color-bg); }
.auth-page__split { display: grid; grid-template-columns: 1fr 1fr; width: 100%; max-width: 1200px; margin: 0 auto; }
.auth-page__panel { display: flex; flex-direction: column; justify-content: center; padding: var(--space-10); gap: var(--space-3); }
.auth-page__brand { display: flex; align-items: center; gap: var(--space-3); margin-bottom: var(--space-5); }
.auth-page__logo { width: 40px; height: 40px; background: var(--color-primary); color: var(--color-text-inverse); border-radius: var(--radius-md); display: inline-flex; align-items: center; justify-content: center; font-weight: var(--font-weight-bold); font-size: 13px; letter-spacing: 0.05em; }
.auth-page__brand-name { font-weight: var(--font-weight-semibold); font-size: var(--font-size-lg); }
.auth-page__brand-sub { color: var(--color-text-subtle); font-size: var(--font-size-sm); }
.auth-page__title { font-size: var(--font-size-2xl); }
.auth-page__subtitle { color: var(--color-text-muted); margin-bottom: var(--space-3); }
.auth-page__form { display: flex; flex-direction: column; gap: var(--space-3); max-width: 440px; }
.auth-page__link { color: var(--color-primary); font-size: var(--font-size-sm); text-align: center; padding: var(--space-2); }
.auth-page__link:hover { text-decoration: underline; }
.auth-page__side { background: linear-gradient(135deg, #1e3a5f 0%, #0f1f30 100%); color: var(--color-text-inverse); display: flex; align-items: center; justify-content: center; padding: var(--space-12); }
.auth-page__side-inner { max-width: 380px; }
.auth-page__side-kicker { font-size: var(--font-size-xs); letter-spacing: 0.15em; text-transform: uppercase; color: rgba(255,255,255,0.55); margin-bottom: var(--space-4); }
.auth-page__side-title { font-size: var(--font-size-3xl); line-height: 1.15; color: #fff; margin-bottom: var(--space-3); }
.auth-page__side-text { color: rgba(255,255,255,0.75); font-size: var(--font-size-lg); }
@media (max-width: 900px) { .auth-page__split { grid-template-columns: 1fr; } .auth-page__side { display: none; } }
</style>
