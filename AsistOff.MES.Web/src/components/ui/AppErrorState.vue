<template>
  <div :class="['app-error-state', { 'app-error-state--compact': compact }]" role="alert">
    <i :class="['app-error-state__icon', icon]" aria-hidden="true"></i>
    <h3 class="app-error-state__title">{{ resolvedTitle }}</h3>
    <p class="app-error-state__message">{{ message }}</p>
    <div class="app-error-state__actions">
      <slot name="actions">
        <AppButton variant="secondary" icon="pi pi-refresh" :loading="loading" @click="emit('retry')">
          {{ $t('common.retry') }}
        </AppButton>
      </slot>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import AppButton from './AppButton.vue';

// Shared error presentation with a working retry (issue #314): list fetch
// failures (including axios timeouts surfaced via extractErrorMessage)
// render this panel instead of failing silently or toast-and-blank. The
// caller owns the retry — usually `table.retry` or a view `refresh`.
const props = withDefaults(defineProps<{
  message: string;
  title?: string;
  icon?: string;
  loading?: boolean;
  compact?: boolean;
}>(), { title: '', icon: 'pi pi-exclamation-triangle', loading: false, compact: false });

const emit = defineEmits<{ (e: 'retry'): void }>();

const { t } = useI18n();
const resolvedTitle = computed(() => props.title || t('errors.loadFailed'));
</script>

<style scoped>
.app-error-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: var(--space-12) var(--space-5);
  min-height: 280px;
  color: var(--color-text-muted);
}
.app-error-state--compact {
  padding: var(--space-6) var(--space-4);
  min-height: 160px;
}
.app-error-state__icon {
  font-size: 40px;
  color: var(--color-danger);
  margin-bottom: var(--space-3);
}
.app-error-state--compact .app-error-state__icon {
  font-size: 28px;
}
.app-error-state__title {
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-semibold);
  color: var(--color-text);
  margin-bottom: var(--space-1);
}
.app-error-state__message {
  max-width: 480px;
  overflow-wrap: anywhere;
}
.app-error-state__actions {
  margin-top: var(--space-4);
  display: flex;
  gap: var(--space-2);
}
</style>
