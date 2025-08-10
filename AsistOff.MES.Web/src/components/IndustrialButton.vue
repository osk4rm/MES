<template>
  <button 
    :class="buttonClasses"
    :disabled="disabled || loading"
    @click="handleClick"
    :type="type"
  >
    <i v-if="loading" class="pi pi-spin pi-spinner"></i>
    <i v-else-if="icon" :class="icon"></i>
    <span v-if="$slots.default || label" class="button-label">
      <slot>{{ label }}</slot>
    </span>
  </button>
</template>

<script setup lang="ts">
import { computed, useSlots } from 'vue';

interface Props {
  variant?: 'primary' | 'secondary' | 'danger' | 'success' | 'warning';
  size?: 'small' | 'medium' | 'large';
  icon?: string;
  label?: string;
  loading?: boolean;
  disabled?: boolean;
  type?: 'button' | 'submit' | 'reset';
  block?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  variant: 'primary',
  size: 'medium',
  type: 'button',
  loading: false,
  disabled: false,
  block: false
});

const emit = defineEmits<{
  click: [event: MouseEvent];
}>();

const slots = useSlots();

const buttonClasses = computed(() => [
  'industrial-btn',
  `industrial-btn--${props.variant}`,
  `industrial-btn--${props.size}`,
  {
    'industrial-btn--loading': props.loading,
    'industrial-btn--disabled': props.disabled,
    'industrial-btn--block': props.block,
    'industrial-btn--icon-only': props.icon && !props.label && !slots.default
  }
]);

const handleClick = (event: MouseEvent) => {
  if (!props.disabled && !props.loading) {
    emit('click', event);
  }
};
</script>

<style scoped>
.industrial-btn {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 10px 16px;
  border: 1px solid transparent;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  text-decoration: none;
  font-family: inherit;
  position: relative;
  overflow: hidden;
  background: linear-gradient(135deg, transparent 0%, rgba(255, 255, 255, 0.05) 100%);
}

.industrial-btn:focus {
  outline: none;
  box-shadow: 0 0 0 2px rgba(252, 145, 58, 0.5);
}

.industrial-btn:active {
  transform: translateY(1px);
}

/* Primary variant */
.industrial-btn--primary {
  background: linear-gradient(135deg, #fc913a 0%, #f9d423 100%);
  color: white;
  border-color: #e88b2f;
  box-shadow: 0 2px 8px rgba(252, 145, 58, 0.3);
}

.industrial-btn--primary:hover:not(:disabled) {
  background: linear-gradient(135deg, #e88b2f 0%, #f5c842 100%);
  box-shadow: 0 4px 12px rgba(252, 145, 58, 0.4);
  transform: translateY(-1px);
}

/* Secondary variant */
.industrial-btn--secondary {
  background: linear-gradient(135deg, #475569 0%, #64748b 100%);
  color: #e2e8f0;
  border-color: #64748b;
  box-shadow: 0 2px 8px rgba(71, 85, 105, 0.3);
}

.industrial-btn--secondary:hover:not(:disabled) {
  background: linear-gradient(135deg, #64748b 0%, #94a3b8 100%);
  color: #f1f5f9;
  box-shadow: 0 4px 12px rgba(71, 85, 105, 0.4);
  transform: translateY(-1px);
}

/* Danger variant */
.industrial-btn--danger {
  background: linear-gradient(135deg, #dc2626 0%, #ef4444 100%);
  color: white;
  border-color: #b91c1c;
  box-shadow: 0 2px 8px rgba(220, 38, 38, 0.3);
}

.industrial-btn--danger:hover:not(:disabled) {
  background: linear-gradient(135deg, #b91c1c 0%, #dc2626 100%);
  box-shadow: 0 4px 12px rgba(220, 38, 38, 0.4);
  transform: translateY(-1px);
}

/* Success variant */
.industrial-btn--success {
  background: linear-gradient(135deg, #059669 0%, #10b981 100%);
  color: white;
  border-color: #047857;
  box-shadow: 0 2px 8px rgba(5, 150, 105, 0.3);
}

.industrial-btn--success:hover:not(:disabled) {
  background: linear-gradient(135deg, #047857 0%, #059669 100%);
  box-shadow: 0 4px 12px rgba(5, 150, 105, 0.4);
  transform: translateY(-1px);
}

/* Warning variant */
.industrial-btn--warning {
  background: linear-gradient(135deg, #d97706 0%, #f59e0b 100%);
  color: white;
  border-color: #b45309;
  box-shadow: 0 2px 8px rgba(217, 119, 6, 0.3);
}

.industrial-btn--warning:hover:not(:disabled) {
  background: linear-gradient(135deg, #b45309 0%, #d97706 100%);
  box-shadow: 0 4px 12px rgba(217, 119, 6, 0.4);
  transform: translateY(-1px);
}

/* Sizes */
.industrial-btn--small {
  padding: 6px 12px;
  font-size: 12px;
  gap: 4px;
}

.industrial-btn--medium {
  padding: 10px 16px;
  font-size: 14px;
  gap: 8px;
}

.industrial-btn--large {
  padding: 14px 20px;
  font-size: 16px;
  gap: 10px;
}

/* States */
.industrial-btn--loading,
.industrial-btn--disabled {
  opacity: 0.6;
  cursor: not-allowed;
  transform: none !important;
}

.industrial-btn--disabled:hover {
  transform: none;
  box-shadow: initial;
}

/* Block button */
.industrial-btn--block {
  width: 100%;
  justify-content: center;
}

/* Icon-only button */
.industrial-btn--icon-only {
  padding: 10px;
}

.industrial-btn--icon-only.industrial-btn--small {
  padding: 6px;
}

.industrial-btn--icon-only.industrial-btn--large {
  padding: 14px;
}

.button-label {
  line-height: 1;
}

/* Industrial glow effect */
.industrial-btn::before {
  content: '';
  position: absolute;
  top: 0;
  left: -100%;
  width: 100%;
  height: 100%;
  background: linear-gradient(90deg, transparent, rgba(255, 255, 255, 0.2), transparent);
  transition: left 0.5s;
}

.industrial-btn:hover::before {
  left: 100%;
}
</style>
