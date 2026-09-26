<template>
  <div>
    <AppPageHeader :title="$t('notFound.title')" icon="pi pi-exclamation-circle" />
    <AppCard>
      <AppEmptyState
        icon="pi pi-map-marker"
        :title="$t('notFound.title')"
        :description="$t('notFound.description')"
      >
        <AppButton variant="primary" icon="pi pi-home" @click="goDashboard">
          {{ $t('notFound.backToDashboard') }}
        </AppButton>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="reload">
          {{ $t('common.reload') }}
        </AppButton>
      </AppEmptyState>
    </AppCard>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router';
import AppPageHeader from '../components/ui/AppPageHeader.vue';
import AppCard from '../components/ui/AppCard.vue';
import AppEmptyState from '../components/ui/AppEmptyState.vue';
import AppButton from '../components/ui/AppButton.vue';

// Guarded not-found view (issue #314): unknown routes land here inside the
// AppShell instead of silently bouncing to the dashboard, so the nav frame
// stays mounted and the user keeps a working way back.
const router = useRouter();

function goDashboard(): void {
  void router.push({ name: 'dashboard' });
}

function reload(): void {
  window.location.reload();
}
</script>
