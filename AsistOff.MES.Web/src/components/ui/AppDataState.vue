<template>
  <!--
    Shared list-region state machine (issue #314). Precedence is fixed so
    every browse view behaves the same: error (with retry) wins over
    loading, loading wins over empty, content renders only when the region
    is healthy and non-empty.
  -->
  <AppErrorState
    v-if="error"
    :message="error"
    :title="errorTitle"
    :loading="loading"
    :compact="compact"
    @retry="emit('retry')"
  >
    <template v-if="$slots['error-actions']" #actions><slot name="error-actions" /></template>
  </AppErrorState>
  <AppLoadingState v-else-if="loading && empty" :label="loadingLabel" />
  <AppEmptyState
    v-else-if="empty"
    :icon="emptyIcon"
    :title="emptyTitle || $t('common.noData')"
    :description="emptyDescription"
  >
    <slot name="empty-actions" />
  </AppEmptyState>
  <slot v-else />
</template>

<script setup lang="ts">
import AppEmptyState from './AppEmptyState.vue';
import AppErrorState from './AppErrorState.vue';
import AppLoadingState from './AppLoadingState.vue';

withDefaults(defineProps<{
  loading?: boolean;
  error?: string | null;
  empty?: boolean;
  compact?: boolean;
  loadingLabel?: string;
  errorTitle?: string;
  emptyIcon?: string;
  emptyTitle?: string;
  emptyDescription?: string;
}>(), {
  loading: false,
  error: null,
  empty: false,
  compact: false,
  loadingLabel: '',
  errorTitle: '',
  emptyIcon: 'pi pi-inbox',
  emptyTitle: '',
  emptyDescription: ''
});

const emit = defineEmits<{ (e: 'retry'): void }>();
</script>
