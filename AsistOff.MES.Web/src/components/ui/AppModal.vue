<template>
  <Teleport to="body">
    <Transition name="app-modal">
      <div
        v-if="open"
        v-bind="$attrs"
        class="app-modal"
        role="dialog"
        aria-modal="true"
        @mousedown.self="onBackdrop"
      >
        <div :class="['app-modal__panel', `app-modal__panel--${size}`]" @mousedown.stop>
          <header v-if="title || $slots.header" class="app-modal__header">
            <slot name="header">
              <h3 class="app-modal__title">{{ title }}</h3>
            </slot>
            <button v-if="closable" type="button" class="app-modal__close" aria-label="Close" @click="emit('close')">
              <i class="pi pi-times"></i>
            </button>
          </header>
          <div class="app-modal__body">
            <slot />
          </div>
          <footer v-if="$slots.footer" class="app-modal__footer">
            <slot name="footer" />
          </footer>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
import { watchEffect } from 'vue';

// The root node is <Teleport>, which renders no DOM element of its own, so
// fallthrough attributes (e.g. data-testid set by callers) would land on the
// Teleport placeholder instead of the visible dialog. Disable the automatic
// fallthrough and bind $attrs explicitly on the dialog overlay above.
defineOptions({ inheritAttrs: false });

const props = withDefaults(defineProps<{
  open: boolean;
  title?: string;
  size?: 'sm' | 'md' | 'lg' | 'xl';
  closable?: boolean;
  closeOnBackdrop?: boolean;
}>(), { size: 'md', closable: true, closeOnBackdrop: true });

const emit = defineEmits<{ (e: 'close'): void }>();

function onBackdrop() {
  if (props.closeOnBackdrop) emit('close');
}

watchEffect(() => {
  if (typeof document !== 'undefined') {
    document.body.style.overflow = props.open ? 'hidden' : '';
  }
});
</script>

<style scoped>
.app-modal {
  position: fixed;
  inset: 0;
  background: rgba(15, 31, 48, 0.45);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: var(--space-4);
}

.app-modal__panel {
  background: var(--color-surface);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-overlay);
  display: flex;
  flex-direction: column;
  max-height: calc(100vh - var(--space-8));
  width: 100%;
}

.app-modal__panel--sm { max-width: 400px; }
.app-modal__panel--md { max-width: 560px; }
.app-modal__panel--lg { max-width: 800px; }
.app-modal__panel--xl { max-width: 1100px; }

.app-modal__header {
  display: flex;
  align-items: center;
  padding: var(--space-4) var(--space-5);
  border-bottom: 1px solid var(--color-divider);
}

.app-modal__title {
  flex: 1;
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-semibold);
}

.app-modal__close {
  color: var(--color-text-muted);
  padding: var(--space-1) var(--space-2);
  border-radius: var(--radius-md);
}
.app-modal__close:hover { background: var(--color-surface-sunken); color: var(--color-text); }

.app-modal__body {
  padding: var(--space-5);
  overflow: auto;
  flex: 1;
}

.app-modal__footer {
  display: flex;
  gap: var(--space-2);
  justify-content: flex-end;
  padding: var(--space-3) var(--space-5);
  border-top: 1px solid var(--color-divider);
  background: var(--color-surface-muted);
  border-radius: 0 0 var(--radius-lg) var(--radius-lg);
}

.app-modal-enter-active, .app-modal-leave-active {
  transition: opacity var(--transition-base);
}
.app-modal-enter-active .app-modal__panel,
.app-modal-leave-active .app-modal__panel {
  transition: transform var(--transition-base);
}
.app-modal-enter-from, .app-modal-leave-to { opacity: 0; }
.app-modal-enter-from .app-modal__panel,
.app-modal-leave-to .app-modal__panel { transform: translateY(8px) scale(0.98); }
</style>
