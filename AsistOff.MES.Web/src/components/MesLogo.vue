<template>
  <div class="mes-logo">
    <div class="logo-icon">
      <i :class="icon"></i>
    </div>
    <div class="logo-text">
      <span class="brand-name">{{ label }}</span>
      <div class="brand-tagline">Manufacturing Excellence</div>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{ icon: string; label: string }>();
</script>

<style scoped>
.mes-logo {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-bottom: 2.5rem;
  padding: 1rem;
  border-radius: 12px;
  background: linear-gradient(135deg, rgba(252, 145, 58, 0.1) 0%, rgba(249, 212, 35, 0.1) 100%);
  border: 1px solid rgba(252, 145, 58, 0.2);
  transition: all 0.3s ease;
  cursor: pointer;
}

.mes-logo:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 25px rgba(252, 145, 58, 0.3);
  background: linear-gradient(135deg, rgba(252, 145, 58, 0.15) 0%, rgba(249, 212, 35, 0.15) 100%);
}

.logo-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 48px;
  height: 48px;
  border-radius: 12px;
  background: linear-gradient(135deg, #fc913a 0%, #f9d423 100%);
  color: #23272b;
  font-size: 1.5rem;
  box-shadow: 0 4px 12px rgba(252, 145, 58, 0.4);
  animation: pulse-glow 2s ease-in-out infinite alternate;
}

.logo-text {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.brand-name {
  font-size: 1.25rem;
  font-weight: 700;
  color: #ffe066;
  background: linear-gradient(135deg, #ffe066 0%, #fc913a 100%);
  background-clip: text;
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  letter-spacing: 1px;
  line-height: 1;
}

.brand-tagline {
  font-size: 0.7rem;
  color: rgba(255, 224, 102, 0.7);
  font-weight: 400;
  letter-spacing: 0.5px;
  text-transform: uppercase;
  line-height: 1;
}

@keyframes pulse-glow {
  0% {
    box-shadow: 0 4px 12px rgba(252, 145, 58, 0.4);
  }
  100% {
    box-shadow: 0 4px 20px rgba(252, 145, 58, 0.6);
  }
}

/* Mobile responsive */
@media (max-width: 768px) {
  .mes-logo {
    gap: 0.75rem;
    padding: 0.75rem;
  }
  
  .logo-icon {
    width: 40px;
    height: 40px;
    font-size: 1.25rem;
  }
  
  .brand-name {
    font-size: 1.1rem;
  }
  
  .brand-tagline {
    font-size: 0.65rem;
  }
}
</style>
