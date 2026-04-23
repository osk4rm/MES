<template>
  <button
    :type="type"
    :class="[
      'app-btn',
      `app-btn--${variant}`,
      `app-btn--${size}`,
      { 'app-btn--block': block, 'app-btn--loading': loading, 'app-btn--icon-only': iconOnly }
    ]"
    :disabled="disabled || loading"
    @click="onClick"
  >
    <span v-if="loading" class="app-btn__spinner" aria-hidden="true"></span>
    <i v-else-if="icon" :class="['app-btn__icon', icon]" aria-hidden="true"></i>
    <span v-if="!iconOnly" class="app-btn__label"><slot /></span>
  </button>
</template>

<script setup lang="ts">
type Variant = 'primary' | 'secondary' | 'ghost' | 'danger' | 'subtle';
type Size = 'sm' | 'md' | 'lg';

const props = withDefaults(defineProps<{
  variant?: Variant;
  size?: Size;
  type?: 'button' | 'submit' | 'reset';
  icon?: string;
  block?: boolean;
  loading?: boolean;
  disabled?: boolean;
  iconOnly?: boolean;
}>(), {
  variant: 'secondary',
  size: 'md',
  type: 'button',
  block: false,
  loading: false,
  disabled: false,
  iconOnly: false
});

void props;

const emit = defineEmits<{ (e: 'click', ev: MouseEvent): void }>();

function onClick(ev: MouseEvent) {
  emit('click', ev);
}
</script>

<style scoped>
.app-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  border: 1px solid transparent;
  border-radius: var(--radius-md);
  font-weight: var(--font-weight-medium);
  line-height: 1;
  white-space: nowrap;
  transition: background var(--transition-fast), border-color var(--transition-fast), color var(--transition-fast), box-shadow var(--transition-fast);
  user-select: none;
}

.app-btn:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

/* sizes */
.app-btn--sm { height: var(--control-height-sm); padding: 0 var(--space-3); font-size: var(--font-size-sm); }
.app-btn--md { height: var(--control-height-md); padding: 0 var(--space-4); font-size: var(--font-size-md); }
.app-btn--lg { height: var(--control-height-lg); padding: 0 var(--space-5); font-size: var(--font-size-lg); }

.app-btn--icon-only.app-btn--sm { width: var(--control-height-sm); padding: 0; }
.app-btn--icon-only.app-btn--md { width: var(--control-height-md); padding: 0; }
.app-btn--icon-only.app-btn--lg { width: var(--control-height-lg); padding: 0; }

.app-btn--block { width: 100%; }

/* variants */
.app-btn--primary {
  background: var(--color-primary);
  color: var(--color-text-inverse);
  border-color: var(--color-primary);
}
.app-btn--primary:not(:disabled):hover { background: var(--color-primary-hover); border-color: var(--color-primary-hover); }
.app-btn--primary:not(:disabled):active { background: var(--color-primary-active); border-color: var(--color-primary-active); }

.app-btn--secondary {
  background: var(--color-surface);
  color: var(--color-text);
  border-color: var(--color-border-strong);
}
.app-btn--secondary:not(:disabled):hover { background: var(--color-surface-sunken); }
.app-btn--secondary:not(:disabled):active { background: var(--color-surface-muted); }

.app-btn--ghost {
  background: transparent;
  color: var(--color-text);
}
.app-btn--ghost:not(:disabled):hover { background: var(--color-surface-sunken); }

.app-btn--subtle {
  background: var(--color-primary-soft);
  color: var(--color-primary);
  border-color: transparent;
}
.app-btn--subtle:not(:disabled):hover { background: var(--color-primary-soft-hover); }

.app-btn--danger {
  background: var(--color-danger);
  color: var(--color-text-inverse);
  border-color: var(--color-danger);
}
.app-btn--danger:not(:disabled):hover { background: #9a1c14; border-color: #9a1c14; }

.app-btn__icon { font-size: 0.95em; }

.app-btn__spinner {
  width: 14px;
  height: 14px;
  border: 2px solid currentColor;
  border-right-color: transparent;
  border-radius: 50%;
  animation: app-btn-spin 0.7s linear infinite;
}

@keyframes app-btn-spin {
  to { transform: rotate(360deg); }
}
</style>
