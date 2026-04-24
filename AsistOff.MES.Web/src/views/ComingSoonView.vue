<template>
  <div>
    <AppPageHeader :title="title" :icon="icon" />
    <AppCard>
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

const route = useRoute();
const { t } = useI18n();
const title = computed(() => {
  const key = route.meta?.titleKey as string | undefined;
  return key ? t(key) : '';
});
const icon = computed(() => (route.meta?.icon as string | undefined) ?? 'pi pi-wrench');
</script>
