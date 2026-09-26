<template>
  <slot v-if="!state.failed" />
  <div v-else class="app-error-fallback" role="alert">
    <i class="pi pi-exclamation-triangle app-error-fallback__icon" aria-hidden="true"></i>
    <h2 class="app-error-fallback__title">{{ $t('errors.boundaryTitle') }}</h2>
    <p class="app-error-fallback__hint">{{ $t('errors.boundaryHint') }}</p>
    <p class="app-error-fallback__correlation">
      {{ $t('errors.correlationId') }}: <code>{{ state.correlationId }}</code>
    </p>
    <AppButton variant="primary" icon="pi pi-refresh" @click="reload">
      {{ $t('common.reload') }}
    </AppButton>
    <details v-if="state.message" class="app-error-fallback__details">
      <summary>{{ $t('errors.details') }}</summary>
      <pre>{{ state.message }}</pre>
    </details>
  </div>
</template>

<script setup lang="ts">
import { reactive, watch, onErrorCaptured } from 'vue';
import { useRoute } from 'vue-router';
import AppButton from './AppButton.vue';
import { generateCorrelationId } from '../../services/correlation';

// Global error boundary (issue #273): a render error in one view must show
// this fallback with a reload action instead of a blank page. The shell
// around the boundary stays mounted, and the correlation id lets support
// match the report with backend logs.
const state = reactive({
  failed: false,
  correlationId: '',
  message: ''
});

const route = useRoute();
watch(
  () => route.fullPath,
  () => {
    state.failed = false;
    state.correlationId = '';
    state.message = '';
  }
);

onErrorCaptured((err) => {
  state.failed = true;
  state.correlationId = generateCorrelationId();
  state.message = err instanceof Error ? err.message : String(err);
  // Stop the error here — App.vue must never go blank.
  return false;
});

function reload(): void {
  window.location.reload();
}
</script>

<style scoped>
.app-error-fallback {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  gap: var(--space-3);
  padding: var(--space-12) var(--space-5);
  min-height: 280px;
  color: var(--color-text-muted);
}
.app-error-fallback__icon {
  font-size: 40px;
  color: var(--color-warning);
}
.app-error-fallback__title {
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-semibold);
  color: var(--color-text);
}
.app-error-fallback__hint {
  max-width: 420px;
}
.app-error-fallback__correlation {
  font-size: var(--font-size-sm);
}
.app-error-fallback__correlation code {
  font-family: monospace;
  color: var(--color-text);
}
.app-error-fallback__details {
  max-width: 560px;
  font-size: var(--font-size-sm);
}
.app-error-fallback__details pre {
  white-space: pre-wrap;
  text-align: left;
  margin-top: var(--space-2);
}
</style>
