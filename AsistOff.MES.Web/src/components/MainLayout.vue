<template>
  <div class="mes-layout">
    <SideNav />
    <main class="mes-main">
      <router-view />
    </main>
  </div>
</template>

<script setup lang="ts">
import SideNav from '../components/SideNav.vue';
</script>

<style scoped>
.mes-layout {
  display: flex;
  height: 100vh;
  width: 100vw;
  background: radial-gradient(ellipse at 60% 40%, #2c3136 60%, #232526 100%);
  font-family: 'Orbitron', 'Roboto Mono', monospace;
  position: fixed;
  top: 0;
  left: 0;
}
.mes-main {
  flex: 1;
  padding: 24px;
  overflow: auto;
}
</style>
