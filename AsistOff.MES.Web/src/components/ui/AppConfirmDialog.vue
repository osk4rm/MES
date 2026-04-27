<template>
  <AppModal :open="open" :title="title" size="sm" @close="emit('cancel')">
    <div class="app-confirm">
      <div :class="['app-confirm__icon', `app-confirm__icon--${variant}`]">
        <i :class="iconClass"></i>
      </div>
      <div>
        <p class="app-confirm__message">{{ message }}</p>
        <p v-if="details" class="app-confirm__details">{{ details }}</p>
      </div>
    </div>
    <template #footer>
      <AppButton variant="ghost" :disabled="loading" @click="emit('cancel')">{{ cancelText || $t('common.cancel') }}</AppButton>
      <AppButton :variant="variant === 'danger' ? 'danger' : 'primary'" :loading="loading" @click="emit('confirm')">
        {{ confirmText || $t('common.confirm') }}
      </AppButton>
    </template>
  </AppModal>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import AppModal from './AppModal.vue';
import AppButton from './AppButton.vue';

const props = withDefaults(defineProps<{
  open: boolean;
  title: string;
  message: string;
  details?: string;
  variant?: 'danger' | 'warning' | 'info';
  confirmText?: string;
  cancelText?: string;
  loading?: boolean;
}>(), { variant: 'danger', loading: false });

const emit = defineEmits<{ (e: 'confirm'): void; (e: 'cancel'): void }>();

const iconClass = computed(() => ({
  danger: 'pi pi-exclamation-triangle',
  warning: 'pi pi-exclamation-circle',
  info: 'pi pi-info-circle'
}[props.variant]));
</script>

<style scoped>
.app-confirm { display: flex; gap: var(--space-4); align-items: flex-start; }
.app-confirm__icon {
  flex-shrink: 0;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
}
.app-confirm__icon--danger { background: var(--color-danger-soft); color: var(--color-danger); }
.app-confirm__icon--warning { background: var(--color-warning-soft); color: var(--color-warning); }
.app-confirm__icon--info { background: var(--color-info-soft); color: var(--color-info); }
.app-confirm__message { color: var(--color-text); font-weight: var(--font-weight-medium); }
.app-confirm__details { color: var(--color-text-muted); font-size: var(--font-size-sm); margin-top: var(--space-1); }
</style>
