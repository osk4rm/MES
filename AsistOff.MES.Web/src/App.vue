<template>
  <AppErrorBoundary>
    <router-view />
  </AppErrorBoundary>
  <AppToastHost />
</template>

<script setup lang="ts">
import AppToastHost from './components/ui/AppToastHost.vue';
import AppErrorBoundary from './components/ui/AppErrorBoundary.vue';
</script>
