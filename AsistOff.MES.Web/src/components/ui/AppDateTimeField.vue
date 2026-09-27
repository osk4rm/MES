<template>
  <AppFormField :label="label" :required="required" :error="error" :hint="hintText">
    <template #default="{ id, invalid }">
      <AppInput
        :id="id"
        :model-value="modelValue"
        type="datetime-local"
        :disabled="disabled"
        :required="required"
        :invalid="invalid"
        :autocomplete="autocomplete"
        @update:modelValue="onUpdate"
        @change="onChange"
        @blur="emit('blur', $event)"
      />
    </template>
  </AppFormField>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import AppFormField from './AppFormField.vue';
import AppInput from './AppInput.vue';

// Shared touch-friendly date-time field (F-06): labelled datetime-local
// entry with 44 px targets (via the shopfloor-view scope or the local
// touch class), an explicit local-time hint, and a UTC conversion note so
// operators know the API persists UTC. Invalid input yields an app-locale
// message through `error` (AppFormField), never the browser-locale native
// bubble alone.
const props = withDefaults(defineProps<{
  modelValue: string | null | undefined;
  label: string;
  required?: boolean;
  disabled?: boolean;
  error?: string | null;
  hint?: string;
  autocomplete?: string;
}>(), {
  required: false,
  disabled: false,
  autocomplete: 'off'
});

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void;
  (e: 'change', value: string): void;
  (e: 'blur', ev: FocusEvent): void;
}>();

const { t } = useI18n();

const localHint = computed(() => t('common.datetimeLocalHint'));
const utcNote = computed(() => t('common.datetimeUtcNote'));

const hintText = computed(() => {
  const parts = [props.hint, localHint.value, utcNote.value].filter((p): p is string => !!p);
  return parts.length > 0 ? parts.join(' ') : undefined;
});

function onUpdate(value: string): void {
  emit('update:modelValue', value);
}

function onChange(value: string): void {
  emit('change', value);
}
</script>

<style scoped>
/* 44 px gloved-operation minimum even outside the shopfloor-view scope. */
:deep(.app-input) {
  min-height: var(--control-height-touch);
}
</style>
