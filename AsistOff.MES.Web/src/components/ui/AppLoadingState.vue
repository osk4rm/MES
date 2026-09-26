<template>
  <div class="app-loading-state" role="status" :aria-label="resolvedLabel">
    <AppSpinner :size="size" />
    <span class="app-loading-state__label">{{ resolvedLabel }}</span>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import AppSpinner from './AppSpinner.vue';

// Shared loading presentation (issue #314): every view shows the same
// spinner + label while its first page loads instead of bare spinners or
// ad-hoc "Loading…" text divs.
const props = withDefaults(defineProps<{
  label?: string;
  size?: 'sm' | 'md' | 'lg';
}>(), { label: '', size: 'md' });

const { t } = useI18n();
const resolvedLabel = computed(() => props.label || t('common.loading'));
</script>

<style scoped>
.app-loading-state {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-3);
  padding: var(--space-10) var(--space-5);
  min-height: 160px;
  color: var(--color-text-muted);
}
.app-loading-state__label {
  font-size: var(--font-size-md);
}
</style>
