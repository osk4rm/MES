<template>
  <div class="industrial-loader-wrapper">
    <div class="industrial-loader">
      <i class="pi pi-cog cog cog1"></i>
      <i class="pi pi-cog cog cog2"></i>
      <i class="pi pi-cog cog cog3"></i>
      <div class="loader-text">Loading MES Platform...</div>
    </div>
  </div>
</template>

<script setup lang="ts">
// No script logic needed for static loader
</script>

<style scoped>
.industrial-loader-wrapper {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  background: linear-gradient(135deg, #232526 0%, #414345 100%);
}
.industrial-loader {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2rem;
}
.cog {
  font-size: 3rem;
  color: #f9d423;
  margin: 0 0.5rem;
  animation: spin 2s linear infinite;
}
.cog1 {
  animation-delay: 0s;
}
.cog2 {
  color: #fc913a;
  animation-delay: 0.3s;
  font-size: 2.5rem;
}
.cog3 {
  color: #ff4e50;
  animation-delay: 0.6s;
  font-size: 2rem;
}
@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}
.loader-text {
  color: #fff;
  font-size: 1.5rem;
  letter-spacing: 2px;
  font-family: 'Orbitron', 'Roboto Mono', monospace;
  text-shadow: 0 2px 8px #000a;
}
</style>
