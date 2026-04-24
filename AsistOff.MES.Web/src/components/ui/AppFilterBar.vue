<template>
  <div class="app-filter-bar">
    <div class="app-filter-bar__fields"><slot /></div>
    <div class="app-filter-bar__actions">
      <slot name="actions" />
      <button type="button" class="app-filter-bar__clear" @click="emit('clear')">
        <i class="pi pi-filter-slash"></i>
        <span>{{ $t('common.clear') }}</span>
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
const emit = defineEmits<{ (e: 'clear'): void }>();
</script>

<style scoped>
.app-filter-bar {
  display: flex;
  align-items: flex-end;
  gap: var(--space-3);
  padding: var(--space-3);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  margin-bottom: var(--space-3);
  flex-wrap: wrap;
}
.app-filter-bar__fields {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-3);
  flex: 1;
}
.app-filter-bar__fields > * { min-width: 180px; flex: 0 0 auto; }
.app-filter-bar__actions { display: inline-flex; gap: var(--space-2); }
.app-filter-bar__clear {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
  height: var(--control-height-md);
  padding: 0 var(--space-3);
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
}
.app-filter-bar__clear:hover { color: var(--color-text); background: var(--color-surface-sunken); }
</style>
