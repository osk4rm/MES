<template>
  <div class="app-row-actions">
    <button
      v-for="a in actions"
      :key="a.key"
      type="button"
      :class="['app-row-actions__btn', { 'app-row-actions__btn--danger': a.variant === 'danger' }]"
      :title="a.label"
      :aria-label="a.label"
      :disabled="a.disabled"
      @click.stop="emit('action', a.key)"
    >
      <i :class="['pi', a.icon]" aria-hidden="true"></i>
    </button>
  </div>
</template>

<script setup lang="ts">
export interface RowAction {
  key: string;
  label: string;
  icon: string;
  variant?: 'default' | 'danger';
  disabled?: boolean;
}
defineProps<{ actions: RowAction[] }>();
const emit = defineEmits<{ (e: 'action', key: string): void }>();
</script>

<style scoped>
.app-row-actions { display: inline-flex; gap: var(--space-1); }
.app-row-actions__btn {
  width: 26px;
  height: 26px;
  border-radius: var(--radius-sm);
  color: var(--color-text-muted);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
}
.app-row-actions__btn:hover:not(:disabled) { background: var(--color-surface-sunken); color: var(--color-text); }
.app-row-actions__btn--danger:hover:not(:disabled) { background: var(--color-danger-soft); color: var(--color-danger); }
.app-row-actions__btn:disabled { opacity: 0.4; cursor: not-allowed; }
</style>
