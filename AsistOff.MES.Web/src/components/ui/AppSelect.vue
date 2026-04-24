<template>
  <select
    :id="id"
    :value="modelValue === null || modelValue === undefined ? '' : String(modelValue)"
    :disabled="disabled"
    :required="required"
    :class="['app-select', { 'app-select--invalid': invalid }]"
    @change="onChange"
    @blur="emit('blur', $event)"
  >
    <option v-if="placeholder" value="" disabled>{{ placeholder }}</option>
    <option v-if="allowEmpty" value="">{{ emptyLabel }}</option>
    <option
      v-for="opt in options"
      :key="String(opt.value)"
      :value="String(opt.value)"
      :disabled="opt.disabled"
    >
      {{ opt.label }}
    </option>
  </select>
</template>

<script setup lang="ts">
export interface SelectOption {
  value: string | number | null;
  label: string;
  disabled?: boolean;
}

const props = withDefaults(defineProps<{
  id?: string;
  modelValue: string | number | null | undefined;
  options: SelectOption[];
  placeholder?: string;
  disabled?: boolean;
  required?: boolean;
  invalid?: boolean;
  allowEmpty?: boolean;
  emptyLabel?: string;
}>(), { disabled: false, required: false, invalid: false, allowEmpty: false, emptyLabel: '—' });

const emit = defineEmits<{
  (e: 'update:modelValue', value: string | number | null): void;
  (e: 'change', value: string | number | null): void;
  (e: 'blur', ev: FocusEvent): void;
}>();

function onChange(ev: Event) {
  const raw = (ev.target as HTMLSelectElement).value;
  let out: string | number | null = raw;
  if (raw === '') {
    out = null;
  } else if (props.options.some(o => typeof o.value === 'number' && String(o.value) === raw)) {
    out = Number(raw);
  }
  emit('update:modelValue', out);
  emit('change', out);
}
</script>

<style scoped>
.app-select {
  width: 100%;
  height: var(--control-height-md);
  padding: 0 var(--space-3);
  padding-right: var(--space-8);
  font-size: var(--font-size-md);
  color: var(--color-text);
  background: var(--color-surface);
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  outline: 0;
  appearance: none;
  background-image: url("data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 20 20'><path fill='%235a6b7d' d='M5 8l5 5 5-5z'/></svg>");
  background-repeat: no-repeat;
  background-position: right var(--space-3) center;
  transition: border-color var(--transition-fast), box-shadow var(--transition-fast);
}
.app-select:hover:not(:disabled) { border-color: var(--color-primary); }
.app-select:focus { border-color: var(--color-primary); box-shadow: 0 0 0 3px var(--color-focus-ring); }
.app-select--invalid { border-color: var(--color-danger); }
.app-select:disabled { background: var(--color-surface-sunken); cursor: not-allowed; }
</style>
