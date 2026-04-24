<template>
  <section class="app-card">
    <header v-if="title || $slots.header" class="app-card__header">
      <slot name="header">
        <h3 class="app-card__title">{{ title }}</h3>
        <p v-if="subtitle" class="app-card__subtitle">{{ subtitle }}</p>
      </slot>
    </header>
    <div :class="['app-card__body', { 'app-card__body--padded': padded }]">
      <slot />
    </div>
    <footer v-if="$slots.footer" class="app-card__footer"><slot name="footer" /></footer>
  </section>
</template>

<script setup lang="ts">
withDefaults(defineProps<{ title?: string; subtitle?: string; padded?: boolean }>(), { padded: true });
</script>

<style scoped>
.app-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  display: flex;
  flex-direction: column;
}
.app-card__header {
  padding: var(--space-4) var(--space-5);
  border-bottom: 1px solid var(--color-divider);
}
.app-card__title { font-size: var(--font-size-lg); font-weight: var(--font-weight-semibold); }
.app-card__subtitle { color: var(--color-text-muted); font-size: var(--font-size-sm); margin-top: var(--space-1); }
.app-card__body--padded { padding: var(--space-5); }
.app-card__footer { padding: var(--space-3) var(--space-5); border-top: 1px solid var(--color-divider); background: var(--color-surface-muted); border-radius: 0 0 var(--radius-lg) var(--radius-lg); }
</style>
