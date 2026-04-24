<template>
  <div :class="['app-input', { 'app-input--invalid': invalid, 'app-input--disabled': disabled }]">
    <i v-if="prefixIcon" :class="['app-input__icon app-input__icon--prefix', prefixIcon]" aria-hidden="true"></i>
    <input
      :id="id"
      :type="type"
      :value="modelValue"
      :placeholder="placeholder"
      :disabled="disabled"
      :readonly="readonly"
      :required="required"
      :autocomplete="autocomplete"
      :inputmode="inputmode"
      :name="name"
      class="app-input__field"
      @input="onInput"
      @change="onChange"
      @blur="emit('blur', $event)"
      @focus="emit('focus', $event)"
      @keydown.enter="emit('enter', $event)"
    />
    <button
      v-if="clearable && modelValue"
      type="button"
      class="app-input__clear"
      aria-label="Clear"
      @click="onClear"
    >
      <i class="pi pi-times"></i>
    </button>
    <i v-if="suffixIcon && !clearable" :class="['app-input__icon app-input__icon--suffix', suffixIcon]" aria-hidden="true"></i>
  </div>
</template>

<script setup lang="ts">
const props = withDefaults(defineProps<{
  id?: string;
  modelValue: string | number | null | undefined;
  type?: string;
  placeholder?: string;
  disabled?: boolean;
  readonly?: boolean;
  required?: boolean;
  invalid?: boolean;
  prefixIcon?: string;
  suffixIcon?: string;
  clearable?: boolean;
  autocomplete?: string;
  inputmode?: 'none' | 'text' | 'decimal' | 'numeric' | 'tel' | 'search' | 'email' | 'url';
  name?: string;
}>(), {
  type: 'text',
  disabled: false,
  readonly: false,
  required: false,
  invalid: false,
  clearable: false
});

void props;

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void;
  (e: 'change', value: string): void;
  (e: 'blur', ev: FocusEvent): void;
  (e: 'focus', ev: FocusEvent): void;
  (e: 'enter', ev: KeyboardEvent): void;
}>();

function onInput(ev: Event) {
  emit('update:modelValue', (ev.target as HTMLInputElement).value);
}
function onChange(ev: Event) {
  emit('change', (ev.target as HTMLInputElement).value);
}
function onClear() {
  emit('update:modelValue', '');
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
  min-width: 0;
}

.app-input:hover:not(.app-input--disabled) { border-color: var(--color-primary); }
.app-input:focus-within {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 3px var(--color-focus-ring);
}

.app-input--invalid { border-color: var(--color-danger); }
.app-input--invalid:focus-within {
  border-color: var(--color-danger);
  box-shadow: 0 0 0 3px rgba(180, 35, 24, 0.2);
}

.app-input--disabled {
  background: var(--color-surface-sunken);
  color: var(--color-text-muted);
  cursor: not-allowed;
}

.app-input__field {
  flex: 1;
  min-width: 0;
  height: 100%;
  padding: 0 var(--space-3);
  background: transparent;
  border: 0;
  outline: 0;
  font-size: var(--font-size-md);
  color: inherit;
}

.app-input__field::placeholder { color: var(--color-text-subtle); }
.app-input__field:disabled { cursor: not-allowed; }

.app-input__icon {
  color: var(--color-text-subtle);
  font-size: 0.9em;
}
.app-input__icon--prefix { padding-left: var(--space-3); }
.app-input__icon--prefix + .app-input__field { padding-left: var(--space-2); }
.app-input__icon--suffix { padding-right: var(--space-3); }

.app-input__clear {
  height: 100%;
  padding: 0 var(--space-2);
  color: var(--color-text-subtle);
  cursor: pointer;
}
.app-input__clear:hover { color: var(--color-text); }
</style>
