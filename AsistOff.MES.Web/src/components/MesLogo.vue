<template>
  <div class="mes-logo">
    <i :class="icon"></i>
    <span>{{ label }}</span>
  </div>
</template>

<script setup lang="ts">
import { defineProps } from 'vue';
const props = defineProps<{ icon: string; label: string }>();
</script>

<style scoped>
.mes-logo {
  display: flex;
  align-items: center;
  gap: 0.7rem;
  font-size: 1.5rem;
  margin-bottom: 2.5rem;
  color: #f9d423;
  font-weight: bold;
  letter-spacing: 2px;
}
</style>
