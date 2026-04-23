<template>
  <div :class="['app-field', { 'app-field--invalid': !!error }]">
    <label v-if="label" :for="inputId" class="app-field__label">
      {{ label }}
      <span v-if="required" class="app-field__required" aria-hidden="true">*</span>
    </label>
    <div class="app-field__control">
      <slot :id="inputId" :invalid="!!error" />
    </div>
    <div v-if="error || hint" class="app-field__message">
      <span v-if="error" class="app-field__error">{{ error }}</span>
      <span v-else-if="hint" class="app-field__hint">{{ hint }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, useId } from 'vue';

const props = withDefaults(defineProps<{
  label?: string;
  required?: boolean;
  error?: string | null;
  hint?: string;
  forId?: string;
}>(), { required: false });

const autoId = useId();
const inputId = computed(() => props.forId || `field-${autoId}`);
</script>

<style scoped>
.app-field {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  min-width: 0;
}

.app-field__label {
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-medium);
  color: var(--color-text-muted);
}

.app-field__required { color: var(--color-danger); margin-left: 2px; }

.app-field__message {
  font-size: var(--font-size-sm);
  min-height: 1em;
}

.app-field__error { color: var(--color-danger); }
.app-field__hint { color: var(--color-text-subtle); }
</style>
