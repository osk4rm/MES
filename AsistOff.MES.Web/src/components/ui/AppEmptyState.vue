<template>
  <div class="app-empty-state">
    <i v-if="icon" :class="['app-empty-state__icon', icon]" aria-hidden="true"></i>
    <h3 v-if="title" class="app-empty-state__title">{{ title }}</h3>
    <p v-if="description" class="app-empty-state__description">{{ description }}</p>
    <div v-if="$slots.default" class="app-empty-state__actions"><slot /></div>
  </div>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
  icon?: string;
  title?: string;
  description?: string;
}>(), { icon: 'pi pi-inbox' });
</script>

<style scoped>
.app-empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: var(--space-12) var(--space-5);
  color: var(--color-text-muted);
  min-height: 280px;
}
.app-empty-state__icon {
  font-size: 40px;
  color: var(--color-text-subtle);
  margin-bottom: var(--space-3);
}
.app-empty-state__title {
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-semibold);
  color: var(--color-text);
  margin-bottom: var(--space-1);
}
.app-empty-state__description { max-width: 420px; }
.app-empty-state__actions { margin-top: var(--space-4); display: flex; gap: var(--space-2); }
</style>
