<template>
  <div class="app-autocomplete">
    <div :class="['app-input', { 'app-input--invalid': invalid, 'app-input--disabled': disabled }]">
      <input
        :id="id"
        type="text"
        :value="inputValue"
        :placeholder="placeholder"
        :disabled="disabled"
        :required="required"
        autocomplete="off"
        class="app-input__field"
        @input="onInput"
        @focus="onFocus"
        @blur="onBlur"
        @keydown.down.prevent="moveDown"
        @keydown.up.prevent="moveUp"
        @keydown.enter.prevent="selectHighlighted"
        @keydown.escape="close"
      />
      <button
        v-if="modelValue && !disabled"
        type="button"
        class="app-input__clear"
        tabindex="-1"
        @mousedown.prevent="clear"
      >
        <i class="pi pi-times"></i>
      </button>
      <i v-else class="pi pi-chevron-down app-input__icon app-input__icon--suffix"></i>
    </div>

    <ul v-if="open && visibleOptions.length > 0" class="app-autocomplete__dropdown" role="listbox">
      <li
        v-for="(opt, idx) in visibleOptions"
        :key="String(opt.value)"
        role="option"
        :class="['app-autocomplete__option', { 'app-autocomplete__option--active': idx === highlightedIdx }]"
        @mousedown.prevent="select(opt)"
        @mouseover="highlightedIdx = idx"
      >
        {{ opt.label }}
      </li>
    </ul>
    <ul v-else-if="open && query.length > 0 && !loading" class="app-autocomplete__dropdown">
      <li class="app-autocomplete__empty">{{ $t('common.noData') }}</li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue';

export interface AutocompleteOption {
  value: string;
  label: string;
}

const props = withDefaults(defineProps<{
  id?: string;
  modelValue: string | null | undefined;
  options: AutocompleteOption[];
  placeholder?: string;
  disabled?: boolean;
  required?: boolean;
  invalid?: boolean;
  loading?: boolean;
}>(), { disabled: false, required: false, invalid: false, loading: false });

const emit = defineEmits<{
  (e: 'update:modelValue', value: string | null): void;
  (e: 'search', query: string): void;
}>();

const open = ref(false);
const query = ref('');
const highlightedIdx = ref(-1);

const selectedOption = computed(() =>
  props.options.find(o => o.value === props.modelValue) ?? null);

const inputValue = computed(() =>
  open.value ? query.value : (selectedOption.value?.label ?? ''));

const visibleOptions = computed(() => {
  const q = query.value.toLowerCase();
  if (!q) return props.options;
  return props.options.filter(o => o.label.toLowerCase().includes(q));
});

watch(() => props.modelValue, () => {
  if (!open.value) query.value = '';
});

function onFocus() {
  open.value = true;
  query.value = selectedOption.value?.label ?? '';
  highlightedIdx.value = -1;
}

function onInput(ev: Event) {
  query.value = (ev.target as HTMLInputElement).value;
  highlightedIdx.value = -1;
  emit('search', query.value);
}

function onBlur() {
  setTimeout(() => {
    open.value = false;
    query.value = '';
  }, 150);
}

function close() {
  open.value = false;
  query.value = '';
}

function select(opt: AutocompleteOption) {
  emit('update:modelValue', opt.value);
  open.value = false;
  query.value = '';
}

function clear() {
  emit('update:modelValue', null);
  query.value = '';
}

function moveDown() {
  if (!open.value) { open.value = true; return; }
  highlightedIdx.value = Math.min(highlightedIdx.value + 1, visibleOptions.value.length - 1);
}

function moveUp() {
  highlightedIdx.value = Math.max(highlightedIdx.value - 1, 0);
}

function selectHighlighted() {
  if (highlightedIdx.value >= 0 && highlightedIdx.value < visibleOptions.value.length) {
    select(visibleOptions.value[highlightedIdx.value]);
  }
}
</script>

<style scoped>
.app-autocomplete {
  position: relative;
}

/* reuse app-input styles */
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
.app-input--disabled { background: var(--color-surface-sunken); cursor: not-allowed; }

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

.app-input__icon { color: var(--color-text-subtle); font-size: 0.9em; }
.app-input__icon--suffix { padding-right: var(--space-3); }

.app-input__clear {
  height: 100%;
  padding: 0 var(--space-2);
  color: var(--color-text-subtle);
  cursor: pointer;
  background: none;
  border: none;
}
.app-input__clear:hover { color: var(--color-text); }

.app-autocomplete__dropdown {
  position: absolute;
  z-index: 1000;
  top: calc(100% + 4px);
  left: 0;
  right: 0;
  background: var(--color-surface);
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-md, 0 4px 12px rgba(0,0,0,.12));
  list-style: none;
  padding: var(--space-1) 0;
  margin: 0;
  max-height: 220px;
  overflow-y: auto;
}

.app-autocomplete__option {
  padding: 8px var(--space-3);
  cursor: pointer;
  font-size: var(--font-size-md);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.app-autocomplete__option:hover,
.app-autocomplete__option--active {
  background: var(--color-primary-soft, #dbeafe);
  color: var(--color-primary, #2563eb);
}

.app-autocomplete__empty {
  padding: 8px var(--space-3);
  color: var(--color-text-muted, #6b7280);
  font-size: var(--font-size-md);
}
</style>
