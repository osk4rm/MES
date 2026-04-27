<template>
  <span :class="['app-spinner', `app-spinner--${props.size}`]" role="status" aria-label="Loading"></span>
</template>

<script setup lang="ts">
const props = withDefaults(defineProps<{ size?: 'sm' | 'md' | 'lg' }>(), {
  size: 'md',
});
</script>

<style scoped>
.app-spinner {
  display: inline-block;
  border: 2px solid var(--color-border-strong);
  border-right-color: var(--color-primary);
  border-radius: 50%;
  animation: app-spinner-rot 0.7s linear infinite;
}
.app-spinner--sm { width: 12px; height: 12px; }
.app-spinner--md, .app-spinner:not([class*='app-spinner--']) { width: 18px; height: 18px; }
.app-spinner--lg { width: 28px; height: 28px; border-width: 3px; }
@keyframes app-spinner-rot { to { transform: rotate(360deg); } }
</style>
