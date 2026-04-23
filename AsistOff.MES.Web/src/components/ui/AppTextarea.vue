<template>
  <textarea
    :id="id"
    :value="modelValue"
    :placeholder="placeholder"
    :disabled="disabled"
    :readonly="readonly"
    :required="required"
    :rows="rows"
    :class="['app-textarea', { 'app-textarea--invalid': invalid }]"
    @input="onInput"
    @blur="emit('blur', $event)"
  />
</template>

<script setup lang="ts">
const props = withDefaults(defineProps<{
  id?: string;
  modelValue: string | null | undefined;
  placeholder?: string;
  disabled?: boolean;
  readonly?: boolean;
  required?: boolean;
  invalid?: boolean;
  rows?: number;
}>(), { rows: 3, invalid: false });
void props;

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void;
  (e: 'blur', ev: FocusEvent): void;
}>();

function onInput(ev: Event) {
  emit('update:modelValue', (ev.target as HTMLTextAreaElement).value);
}
</script>

<style scoped>
.app-textarea {
  width: 100%;
  padding: var(--space-2) var(--space-3);
  font-family: inherit;
  font-size: var(--font-size-md);
  color: var(--color-text);
  background: var(--color-surface);
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  outline: 0;
  resize: vertical;
  transition: border-color var(--transition-fast), box-shadow var(--transition-fast);
}
.app-textarea:hover:not(:disabled) { border-color: var(--color-primary); }
.app-textarea:focus { border-color: var(--color-primary); box-shadow: 0 0 0 3px var(--color-focus-ring); }
.app-textarea--invalid { border-color: var(--color-danger); }
.app-textarea::placeholder { color: var(--color-text-subtle); }
.app-textarea:disabled { background: var(--color-surface-sunken); cursor: not-allowed; }
</style>
