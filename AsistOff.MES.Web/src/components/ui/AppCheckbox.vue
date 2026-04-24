<template>
  <label :class="['app-checkbox', { 'app-checkbox--disabled': disabled }]">
    <input
      type="checkbox"
      :checked="modelValue"
      :disabled="disabled"
      class="app-checkbox__input"
      @change="onChange"
    />
    <span class="app-checkbox__box" aria-hidden="true">
      <i v-if="modelValue" class="pi pi-check"></i>
    </span>
    <span v-if="label || $slots.default" class="app-checkbox__label">
      <slot>{{ label }}</slot>
    </span>
  </label>
</template>

<script setup lang="ts">
defineProps<{
  modelValue: boolean;
  label?: string;
  disabled?: boolean;
}>();

const emit = defineEmits<{ (e: 'update:modelValue', value: boolean): void }>();

function onChange(ev: Event) {
  emit('update:modelValue', (ev.target as HTMLInputElement).checked);
}
</script>

<style scoped>
.app-checkbox {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  cursor: pointer;
  user-select: none;
  font-size: var(--font-size-md);
  color: var(--color-text);
}

.app-checkbox--disabled { cursor: not-allowed; opacity: 0.6; }

.app-checkbox__input {
  position: absolute;
  width: 1px;
  height: 1px;
  opacity: 0;
  pointer-events: none;
}

.app-checkbox__box {
  width: 16px;
  height: 16px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-sm);
  background: var(--color-surface);
  color: var(--color-text-inverse);
  font-size: 10px;
  transition: background var(--transition-fast), border-color var(--transition-fast);
}

.app-checkbox__input:checked + .app-checkbox__box {
  background: var(--color-primary);
  border-color: var(--color-primary);
}

.app-checkbox__input:focus-visible + .app-checkbox__box {
  box-shadow: 0 0 0 3px var(--color-focus-ring);
}

.app-checkbox__label { line-height: 1.2; }
</style>
