<template>
  <div :class="['app-input', { 'app-input--invalid': invalid, 'app-input--disabled': disabled }]">
    <input
      :id="id"
      type="number"
      :value="modelValue ?? ''"
      :placeholder="placeholder"
      :disabled="disabled"
      :readonly="readonly"
      :required="required"
      :min="min"
      :max="max"
      :step="step"
      class="app-input__field"
      @input="onInput"
      @blur="emit('blur', $event)"
    />
    <span v-if="suffix" class="app-input__suffix">{{ suffix }}</span>
  </div>
</template>

<script setup lang="ts">
const props = withDefaults(defineProps<{
  id?: string;
  modelValue: number | null | undefined;
  placeholder?: string;
  disabled?: boolean;
  readonly?: boolean;
  required?: boolean;
  invalid?: boolean;
  min?: number;
  max?: number;
  step?: number | string;
  suffix?: string;
}>(), { step: 1 });
void props;

const emit = defineEmits<{
  (e: 'update:modelValue', value: number | null): void;
  (e: 'blur', ev: FocusEvent): void;
}>();

function onInput(ev: Event) {
  const v = (ev.target as HTMLInputElement).value;
  emit('update:modelValue', v === '' ? null : Number(v));
}
</script>

<style scoped>
.app-input {
  display: flex;
  align-items: center;
  background: var(--color-surface);
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  height: var(--control-height-md);
  transition: border-color var(--transition-fast), box-shadow var(--transition-fast);
}
.app-input:hover:not(.app-input--disabled) { border-color: var(--color-primary); }
.app-input:focus-within { border-color: var(--color-primary); box-shadow: 0 0 0 3px var(--color-focus-ring); }
.app-input--invalid { border-color: var(--color-danger); }
.app-input--disabled { background: var(--color-surface-sunken); cursor: not-allowed; }

.app-input__field {
  flex: 1;
  min-width: 0;
  height: 100%;
  padding: 0 var(--space-3);
  border: 0;
  background: transparent;
  outline: 0;
  font-size: var(--font-size-md);
  color: inherit;
}

.app-input__suffix {
  padding: 0 var(--space-3);
  color: var(--color-text-subtle);
  font-size: var(--font-size-sm);
}
</style>
