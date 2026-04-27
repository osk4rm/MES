<template>
  <header class="app-page-header">
    <div class="app-page-header__left">
      <AppBreadcrumbs v-if="breadcrumbs?.length" :items="breadcrumbs" />
      <h1 class="app-page-header__title">
        <i v-if="icon" :class="['app-page-header__icon', icon]" aria-hidden="true"></i>
        {{ title }}
      </h1>
      <p v-if="subtitle" class="app-page-header__subtitle">{{ subtitle }}</p>
    </div>
    <div v-if="$slots.actions" class="app-page-header__actions">
      <slot name="actions" />
    </div>
  </header>
</template>

<script setup lang="ts">
import AppBreadcrumbs, { type Crumb } from './AppBreadcrumbs.vue';

defineProps<{
  title: string;
  subtitle?: string;
  icon?: string;
  breadcrumbs?: Crumb[];
}>();
</script>

<style scoped>
.app-page-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-4);
  padding-bottom: var(--space-4);
  margin-bottom: var(--space-5);
  border-bottom: 1px solid var(--color-divider);
}

.app-page-header__left { min-width: 0; }

.app-page-header__title {
  font-size: var(--font-size-2xl);
  font-weight: var(--font-weight-semibold);
  display: flex;
  align-items: center;
  gap: var(--space-2);
  color: var(--color-text);
}

.app-page-header__icon { color: var(--color-primary); font-size: 0.9em; }

.app-page-header__subtitle {
  color: var(--color-text-muted);
  font-size: var(--font-size-md);
  margin-top: var(--space-1);
}

.app-page-header__actions { display: flex; gap: var(--space-2); flex-shrink: 0; }
</style>
