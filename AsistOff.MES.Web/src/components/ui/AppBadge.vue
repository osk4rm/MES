<template>
  <span :class="['app-badge', `app-badge--${variant}`, { 'app-badge--dot': dot }]">
    <span v-if="dot" class="app-badge__dot" aria-hidden="true"></span>
    <slot />
  </span>
</template>

<script setup lang="ts">
type Variant = 'neutral' | 'primary' | 'success' | 'warning' | 'danger' | 'info' | 'idle';
defineProps<{ variant?: Variant; dot?: boolean }>();
</script>

<style scoped>
.app-badge {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
  padding: 2px var(--space-2);
  font-size: var(--font-size-xs);
  font-weight: var(--font-weight-medium);
  line-height: 1.4;
  border-radius: var(--radius-sm);
  text-transform: uppercase;
  letter-spacing: 0.03em;
  white-space: nowrap;
}

.app-badge__dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: currentColor;
}

.app-badge--neutral { background: var(--color-surface-sunken); color: var(--color-text-muted); }
.app-badge--primary { background: var(--color-primary-soft); color: var(--color-primary); }
.app-badge--success { background: var(--color-success-soft); color: var(--color-success); }
.app-badge--warning { background: var(--color-warning-soft); color: var(--color-warning); }
.app-badge--danger  { background: var(--color-danger-soft); color: var(--color-danger); }
.app-badge--info    { background: var(--color-info-soft); color: var(--color-info); }
.app-badge--idle    { background: var(--color-idle-soft); color: var(--color-idle); }
</style>
