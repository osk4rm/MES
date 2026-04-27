<template>
  <Teleport to="body">
    <div class="app-toast-host" role="region" aria-live="polite" aria-label="Notifications">
      <TransitionGroup name="app-toast">
        <div
          v-for="t in store.toasts"
          :key="t.id"
          :class="['app-toast', `app-toast--${t.variant}`]"
          role="alert"
        >
          <i :class="['app-toast__icon', iconClass(t.variant)]" aria-hidden="true"></i>
          <span class="app-toast__message">{{ t.message }}</span>
          <button class="app-toast__close" aria-label="Dismiss" @click="store.dismiss(t.id)">
            <i class="pi pi-times"></i>
          </button>
        </div>
      </TransitionGroup>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { useToastStore, type ToastVariant } from '../../stores/toastStore';

const store = useToastStore();

function iconClass(variant: ToastVariant) {
  switch (variant) {
    case 'success': return 'pi pi-check-circle';
    case 'error': return 'pi pi-times-circle';
    case 'warning': return 'pi pi-exclamation-triangle';
    case 'info': return 'pi pi-info-circle';
  }
}
</script>

<style scoped>
.app-toast-host {
  position: fixed;
  top: var(--space-4);
  right: var(--space-4);
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  z-index: 2000;
  max-width: 420px;
}

.app-toast {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-3) var(--space-4);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-left: 3px solid var(--color-text-subtle);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-lg);
  min-width: 280px;
}

.app-toast__icon { font-size: 16px; flex-shrink: 0; }
.app-toast__message { flex: 1; font-size: var(--font-size-md); color: var(--color-text); }
.app-toast__close { color: var(--color-text-subtle); padding: 0 var(--space-1); }
.app-toast__close:hover { color: var(--color-text); }

.app-toast--success { border-left-color: var(--color-success); }
.app-toast--success .app-toast__icon { color: var(--color-success); }
.app-toast--error { border-left-color: var(--color-danger); }
.app-toast--error .app-toast__icon { color: var(--color-danger); }
.app-toast--warning { border-left-color: var(--color-warning); }
.app-toast--warning .app-toast__icon { color: var(--color-warning); }
.app-toast--info { border-left-color: var(--color-info); }
.app-toast--info .app-toast__icon { color: var(--color-info); }

.app-toast-enter-active, .app-toast-leave-active { transition: all var(--transition-base); }
.app-toast-enter-from { opacity: 0; transform: translateX(20px); }
.app-toast-leave-to { opacity: 0; transform: translateX(20px); }
</style>
